using Newtonsoft.Json.Linq;
using SmICSCoreLib.AQL.General;
using SmICSCoreLib.AQL.PatientInformation.Patient_Labordaten.ReceiveModel;
using SmICSCoreLib.REST;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace SmICSCoreLib.AQL.PatientInformation.Patient_Labordaten
{
    public class PatientLabordataFactory : IPatientLabordataFactory
    {
        protected IRestDataAccess _restData;
        public PatientLabordataFactory(IRestDataAccess restData)
        {
            _restData = restData;
        }
        public List<LabDataModel> Process(PatientListParameter parameter)
        {
            List<LabDataReceiveModel> receiveLabDataList = _restData.AQLQuery<LabDataReceiveModel>(AQLCatalog.PatientLaborData(parameter).Query);

            if (receiveLabDataList is null)
            {
                return new List<LabDataModel>();
            }

            return LabDataConstructor(receiveLabDataList);


        }
        private List<LabDataModel> LabDataConstructor(List<LabDataReceiveModel> receiveLabDataList)
        {
            List<LabDataModel> labDataModels = new List<LabDataModel>();
            foreach (LabDataReceiveModel labData in receiveLabDataList)
            {
                labDataModels.Add(new LabDataModel(labData));
            }
            return labDataModels;
        }
        
    }
}
