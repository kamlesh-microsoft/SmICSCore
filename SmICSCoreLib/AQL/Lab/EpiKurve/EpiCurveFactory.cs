using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using SmICSCoreLib.Util;
using Newtonsoft.Json;
using SmICSCoreLib.AQL.General;
using SmICSCoreLib.AQL.Lab.EpiKurve.ReceiveModel;
using SmICSCoreLib.AQL.Lab.EpiKurve;
using SmICSCoreLib.REST;

namespace SmICSCoreLib.AQL.Lab.EpiKurve
{
    public class EpiCurveFactory : IEpiCurveFactory
    {
        private readonly string COMPLETE_CLINIC = "klinik";
        private readonly int WEEK = 7;
        private readonly int MONTH = 28;

        private List<EpiCurveModel> epiCurveList;
        private SortedDictionary<DateTime, Dictionary<string, EpiCurveModel>> dataAggregationStorage;
        private IRestDataAccess _restData;
        private Dictionary<string, PatientInfectionModel> infections;
        private Dictionary<string, EpiCurveModel> EpiCurveEntryByWard;

        private Dictionary<string, List<int>> mavg7;
        private Dictionary<string, List<int>> mavg28;

        public EpiCurveFactory(IRestDataAccess restData)
        {
            _restData = restData;
        }
        public List<EpiCurveModel> Process(EpiCurveParameter parameter)
        {
            InitializeGlobalVariables();

            for (DateTime date = parameter.Starttime.Date; date <= parameter.Endtime.Date; date = date.AddDays(1.0))
            {
                CreateDailyEntries(date, parameter.PathogenName);
                CreateEmptyWardEntries(date);
            }

            AddMissingValues(parameter);
            DataAggregationStorageToList();

            return epiCurveList;
        }

        private void InitializeGlobalVariables()
        {
            dataAggregationStorage = new SortedDictionary<DateTime, Dictionary<string, EpiCurveModel>>();
            epiCurveList = new List<EpiCurveModel>();
            EpiCurveEntryByWard = new Dictionary<string, EpiCurveModel>();
            infections = new Dictionary<string, PatientInfectionModel>();
            mavg28 = new Dictionary<string, List<int>>();
            mavg7 = new Dictionary<string, List<int>>();
            mavg7.Add(COMPLETE_CLINIC, new List<int>());
            mavg28.Add(COMPLETE_CLINIC, new List<int>());
        }
        private void CreateDailyEntries(DateTime date, string pathogen)
        {
            System.Diagnostics.Debug.WriteLine("Flag - Query: " + date.ToString());
            List<FlagTimeModel> flagTimes = _restData.AQLQuery<FlagTimeModel>(AQLCatalog.LaborEpiCurve(date, pathogen).Query);

            if (flagTimes == null)
            {
                AddToEpiCurveToSortedDict(date);
                return;
            }

            PopulateDailyEpicCurve(flagTimes, date);

            AddToEpiCurveToSortedDict(date);

            
        }
        private void AddToEpiCurveToSortedDict(DateTime date)
        {
            dataAggregationStorage.Add(date, EpiCurveEntryByWard);
        }
        private void PopulateDailyEpicCurve(List<FlagTimeModel> flagTimes, DateTime date)
        {
            foreach (FlagTimeModel flag in flagTimes)
            {
                System.Diagnostics.Debug.WriteLine("PatientLocation - Query: " + flag.PatientID + " - " + date.ToString());
                
                PatientLocation patientLocation = _restData.AQLQuery<PatientLocation>(AQLCatalog.PatientLocation(flag.Datum, flag.PatientID).Query)[0];

                if(patientLocation == null)
                {
                    patientLocation = new PatientLocation() { Ward = "ohne Stationsangabe", Departement = "0000" };
                }

                SetBasicDailyEpiCurveEntries(flag, patientLocation, date);
                AggregateFlagInformation(flag, patientLocation);

            }
            GetMovingAverages();
        }
        private void SetBasicDailyEpiCurveEntries(FlagTimeModel flag, PatientLocation patientLocation, DateTime date)
        { 
            if (!EpiCurveEntryByWard.ContainsKey(COMPLETE_CLINIC))
            {
                EpiCurveEntryByWard.Add(COMPLETE_CLINIC, InitializeNewEpiCurveModel(flag, COMPLETE_CLINIC, date));
            }
            if (!EpiCurveEntryByWard.ContainsKey(patientLocation.Ward) && flag.HasFlag())
            {
                EpiCurveEntryByWard.Add(patientLocation.Ward, InitializeNewEpiCurveModel(flag, patientLocation.Ward, date));
            }
            if(!mavg28.ContainsKey(patientLocation.Ward) && !mavg7.ContainsKey(patientLocation.Ward))
            {
                mavg7.Add(patientLocation.Ward, new List<int>());
                mavg28.Add(patientLocation.Ward, new List<int>());
            }
        }
        private void AggregateFlagInformation(FlagTimeModel flag, PatientLocation patientLocation)
        {
            if (infections.ContainsKey(flag.PatientID))
            {
                PatientInfectionModel patientInfections = infections[flag.PatientID];

                if (patientInfections.IsInfected && !patientInfections.HasFirstNegativeTest && !flag.HasFlag())
                {
                    patientInfections.HasFirstNegativeTest = true;
                }
                else if (patientInfections.IsInfected && patientInfections.HasFirstNegativeTest && !flag.HasFlag())
                {
                    DecrementOverallCount(patientInfections.InfectionWard);
                    DecrementOverallCount(COMPLETE_CLINIC);
                }
            }
            else
            {
                InitializeNewInfectiousPatient(flag, patientLocation);
                if (flag.HasFlag())
                {
                    IncrementCounts(patientLocation.Ward);
                    IncrementCounts(COMPLETE_CLINIC);

                }
            }
        }
        private void GetMovingAverages()
        {
            foreach(KeyValuePair<string, EpiCurveModel> keyValuePair in EpiCurveEntryByWard)
            {
                keyValuePair.Value.MAVG7 = MovingAverage.Calculate(mavg7[keyValuePair.Key], keyValuePair.Value.Anzahl, WEEK);
                keyValuePair.Value.MAVG28 = MovingAverage.Calculate(mavg28[keyValuePair.Key], keyValuePair.Value.Anzahl, MONTH);
            }
        }
        private void DecrementOverallCount(string ward)
        {
            EpiCurveEntryByWard[ward].anzahl_gesamt -= 1;
        }
        private void IncrementCounts(string ward)
        {
            EpiCurveEntryByWard[ward].Anzahl += 1;
            EpiCurveEntryByWard[ward].anzahl_gesamt += 1;
        }
        private void DataAggregationStorageToList()
        {
            foreach(KeyValuePair<DateTime, Dictionary<string, EpiCurveModel>> keyValuePair in dataAggregationStorage)
            {
                epiCurveList.AddRange(dataAggregationStorage[keyValuePair.Key].Values);
            }
        }
        private void AddMissingValues(TimespanParameter parameter)
        {
            ICollection<string> wards = dataAggregationStorage[parameter.Endtime.Date].Keys;
            DateTime date = parameter.Starttime.Date;
            while (dataAggregationStorage[date].Keys.Count != wards.Count)
            {
                foreach (string ward in wards)
                {
                    if (!dataAggregationStorage[date].ContainsKey(ward))
                    {
                        dataAggregationStorage[date].Add(ward, InitializeEmptyEpiCurveModel(dataAggregationStorage[parameter.Endtime.Date][ward], date, Purpose.FOR_PAST));
                    }
                }
                
                date = date.AddDays(1);
            }
        }
        private void CreateEmptyWardEntries(DateTime date)
        {
            Dictionary<string, EpiCurveModel> nextDayEntries = new Dictionary<string, EpiCurveModel>();
            
            foreach(KeyValuePair<string, EpiCurveModel> keyValuePair in dataAggregationStorage[date])
            {
                nextDayEntries.Add(keyValuePair.Key, InitializeEmptyEpiCurveModel(keyValuePair.Value, date.AddDays(1), Purpose.FOR_FUTURE));
            }

            EpiCurveEntryByWard = nextDayEntries;
        }
        private void InitializeDailyEpiCurve(FlagTimeModel firstFlag, DateTime date)
        {
            EpiCurveEntryByWard = new Dictionary<string, EpiCurveModel>();
            EpiCurveEntryByWard.Add(COMPLETE_CLINIC, InitializeNewEpiCurveModel(firstFlag, COMPLETE_CLINIC, date));
        }
        private void InitializeNewInfectiousPatient(FlagTimeModel flag, PatientLocation loc)
        {
            infections.Add(flag.PatientID, new PatientInfectionModel
            {
                PatientID = flag.PatientID, IsInfected = flag.HasFlag(), InfectionWard = loc.Ward
            });

        }
        private EpiCurveModel InitializeNewEpiCurveModel(FlagTimeModel flag, string ward, DateTime date)
        {
            return new EpiCurveModel()
            {
                ErregerID = flag.VirusCode,
                ErregerBEZL = flag.Virus,
                Anzahl = 0,
                anzahl_gesamt = 0,
                Datum = date,
                StationID = ward
            };
        }
        private EpiCurveModel InitializeEmptyEpiCurveModel(EpiCurveModel oldModel, DateTime date, Purpose purpose)
        {
            return new EpiCurveModel()
            {
                Anzahl = 0,
                anzahl_gesamt = purpose == Purpose.FOR_FUTURE ? oldModel.anzahl_gesamt : 0,
                Anzahl_cs = 0,
                anzahl_gesamt_av28 = 0,
                anzahl_gesamt_av7 = 0,
                MAVG28 = 0,
                MAVG28_cs = 0,
                MAVG7 = 0,
                MAVG7_cs = 0,
                Datum = date,
                ErregerBEZK = oldModel.ErregerBEZK,
                ErregerBEZL = oldModel.ErregerBEZL,
                ErregerID = oldModel.ErregerID,
                StationID = oldModel.StationID
            };
        }




        #region old code
        /*  private void TemporaryDataStorageConstructor(TimespanParameter parameter)
          {
              SortedDictionary<DateTime, int> active = new SortedDictionary<DateTime, int>();
              SortedDictionary<DateTime, int> sum = new SortedDictionary<DateTime, int>();

              //Prefills the dictionaries
              for (DateTime date = parameter.Starttime.Date; date.Date <= parameter.Endtime.Date; date = date.AddDays(1))
              {
                  active.Add(date, 0);
                  sum.Add(date, 0);
              }

              dataAggregationStorage = new Dictionary<string, SortedDictionary<DateTime, int>> { { "active", active }, { "sum", sum } };
          }

          private void DataAggregation(List<FlagTimeModel> positiveFlagTimes, List<FlagTimeModel> negativeFlagTimes)
          {
              foreach (FlagTimeModel flagTime in positiveFlagTimes)
              {
                  dataAggregationStorage["active"][flagTime.Zeitpunkt.Date] += flagTime.Flag ? 1 : 0;
                  dataAggregationStorage["sum"][flagTime.Zeitpunkt.Date] += flagTime.Flag ? 1 : 0;
              }

              foreach (FlagTimeModel flagTime in negativeFlagTimes)
              {
                  dataAggregationStorage["sum"][flagTime.Zeitpunkt.Date] -= flagTime.Flag ? 1 : 0;
              }
          }

          private void ReturnValueConstructor(TimespanParameter timespan)
          {
              epiCurveList = new List<EpiCurveModel>();
              List<int> last7days = new List<int>();
              List<int> last7days_overall = new List<int>();
              List<int> last28days = new List<int>();
              List<int> last28days_overall = new List<int>();

              for (DateTime date = timespan.Starttime.Date; date.Date <= timespan.Endtime.Date; date = date.AddDays(1))
              {
                  if (date.Date < timespan.Endtime.Date)
                  {
                      dataAggregationStorage["sum"][date.AddDays(1.0)] += dataAggregationStorage["sum"][date];
                  }

                  EpiCurveModel epiModel = new EpiCurveModel();
                  epiModel.Datum = date;
                  epiModel.ErregerID = "COV";
                  epiModel.ErregerBEZK = "Sars-CoV-2";
                  epiModel.Anzahl = dataAggregationStorage["active"][date];
                  epiModel.Anzahl_cs = 0;
                  epiModel.MAVG7 = MovingAverage.Calculate(last7days, dataAggregationStorage["active"][date], 7);
                  epiModel.MAVG28 = MovingAverage.Calculate(last28days, dataAggregationStorage["active"][date], 28); ;
                  epiModel.anzahl_gesamt = dataAggregationStorage["sum"][date]; ;
                  epiModel.anzahl_gesamt_av7 = MovingAverage.Calculate(last7days_overall, dataAggregationStorage["sum"][date], 7); ;
                  epiModel.anzahl_gesamt_av28 = MovingAverage.Calculate(last28days_overall, dataAggregationStorage["sum"][date], 28); ;
                  epiModel.StationID = "klinik";

                  epiCurveList.Add(epiModel);
              };
          }
      }*/
        #endregion
    }
    internal enum Purpose
    {
        FOR_FUTURE,
        FOR_PAST
    }
}
