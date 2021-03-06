using SmICSCoreLib.AQL.Contact_Nth_Network;
using SmICSCoreLib.AQL.General;
using SmICSCoreLib.AQL.Lab.EpiKurve;
using SmICSCoreLib.AQL.PatientInformation.Patient_Mibi_Labordaten.ReceiveModel;
using SmICSCoreLib.AQL.PatientInformation.PatientMovement;
using SmICSCoreLib.Util;
using System;
using System.Collections;
using System.Text.RegularExpressions;

namespace SmICSCoreLib.AQL
{
    public sealed class AQLCatalog
    {
        private AQLCatalog() { }        

        public static AQLQuery ConnectionTest(IDictionary parameter)
        {
            return new AQLQuery($"SELECT count(e/ehr_id/value) as EHR_Count FROM EHR e");
        }

        public static AQLQuery GetEHRID(string subjectID)
        {
            return new AQLQuery($"SELECT DISTINCT e/ehr_id/value as PatientID FROM EHR e CONTAINS COMPOSITION c WHERE e/ehr_status/subject/external_ref/id/value='{subjectID}' and e/ehr_status/subject/external_ref/namespace='SmICSTests'");
        }
        public static AQLQuery ContactPatientWards(ContactParameter parameter)
        {
            return new AQLQuery($@"SELECT m/data[at0001]/items[at0004]/value/value as Beginn, 
                                m/data[at0001]/items[at0005]/value/value as Ende, 
                                o/items[at0024]/value/defining_code/code_string as Fachabteilung, 
                                k/items[at0027]/value/value as StationID 
                                FROM EHR e 
                                CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.event_summary.v0] 
                                CONTAINS ADMIN_ENTRY m[openEHR-EHR-ADMIN_ENTRY.hospitalization.v0] 
                                CONTAINS (CLUSTER k[openEHR-EHR-CLUSTER.location.v1] and CLUSTER o[openEHR-EHR-CLUSTER.organization.v0])
                                WHERE c/name/value='Patientenaufenthalt' 
                                and e/ehr_id/value = '{ parameter.PatientID }' 
                                and m/data[at0001]/items[at0004]/value/value <= '{ parameter.Endtime.ToString("o") }' 
                                and (m/data[at0001]/items[at0005]/value/value >= '{ parameter.Starttime.ToString("o") }'
                                or NOT EXISTS m/data[at0001]/items[at0005]/value/value)
                                ORDER BY m/data[at0001]/items[at0004]/value/value ASC");
        }
        public static AQLQuery ContactPatients(ContactPatientsParameter parameter)
        {
            return new AQLQuery($@"SELECT e/ehr_id/value as PatientID,
                                h/data[at0001]/items[at0004]/value/value as Beginn, 
                                h/data[at0001]/items[at0005]/value/value as Ende FROM EHR e 
                                CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.event_summary.v0] 
                                CONTAINS ADMIN_ENTRY h[openEHR-EHR-ADMIN_ENTRY.hospitalization.v0] 
                                CONTAINS (CLUSTER l[openEHR-EHR-CLUSTER.location.v1] and CLUSTER o[openEHR-EHR-CLUSTER.organization.v0])
                                WHERE c/name/value='Patientenaufenthalt' 
                                and h/data[at0001]/items[at0004]/value/value <= '{ parameter.Endtime.ToString("o") }' 
                                and (h/data[at0001]/items[at0004]/value/value >= '{ parameter.Starttime.ToString("o") }'
                                or NOT EXISTS h/data[at0001]/items[at0005]/value/value)
                                and o/items[at0024]/value/defining_code/code_string = '{ parameter.Departement }' 
                                and l/items[at0027]/value/value = '{ parameter.WardID }' 
                                and not e/ehr_id/value = '{ parameter.PatientID }'");
        }
        /*public static AQLQuery PatientStay(PatientListParameter patientList)
        {
            return new AQLQuery($"SELECT u/data[at0001]/items[at0004]/value/value as Beginn, u/data[at0001]/items[at0005]/value/value as Ende, u/data[at0001]/items[at0006]/value/value as Bewegungsart_l, a/items[at0048]/value/defining_code/code_string as Fachabteilung, e/ehr_id/value as PatientID, n/items[at0001,'Zugehörige Versorgungsfall-Kennung']/value/value as FallID, a/items[at0029,'Zimmerkennung']/value/value as Raum, a/items[at0046]/value/value as StationID FROM EHR e CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.event_summary.v0] CONTAINS (ADMIN_ENTRY u[openEHR-EHR-ADMIN_ENTRY.hospitalization.v0] CONTAINS CLUSTER a[openEHR-EHR-CLUSTER.location.v1] and CLUSTER n[openEHR-EHR-CLUSTER.case_identification.v0]) WHERE c/name/value='Patientenaufenthalt' and e/ehr_id/value MATCHES { patientList.ToAQLMatchString() } and EXISTS n/items[at0001,'Zugehörige Versorgungsfall-Kennung']/value/value ORDER BY e/ehr_id/value ASC, u/data[at0001]/items[at0004]/value/value ASC");
        }*/
        public static AQLQuery PatientStay(PatientListParameter patientList)
        {
            return new AQLQuery($@"SELECT e/ehr_id/value as PatientID,
                                i/items[at0001]/value/value as FallID,
                                h/data[at0001]/items[at0004]/value/value as Beginn,
                                h/data[at0001]/items[at0005]/value/value as Ende,
                                h/data[at0001]/items[at0006]/value/value as Bewegungsart_l,
                                s/items[at0027]/value/value as StationID,
                                s/items[at0029]/value/value as Raum,
                                f/items[at0024]/value/value as Fachabteilung,
                                f/items[at0024]/value/defining_code/code_string as FachabteilungsID
                                FROM EHR e 
                                CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.event_summary.v0] 
                                    CONTAINS (CLUSTER i[openEHR-EHR-CLUSTER.case_identification.v0]
                                    AND ADMIN_ENTRY h[openEHR-EHR-ADMIN_ENTRY.hospitalization.v0]
                                        CONTAINS (CLUSTER s[openEHR-EHR-CLUSTER.location.v1]
                                        AND CLUSTER f[openEHR-EHR-CLUSTER.organization.v0]))
                                WHERE c/name/value = 'Patientenaufenthalt'
                                AND i/items[at0001]/name/value = 'Zugehöriger Versorgungsfall (Kennung)'
                                AND e/ehr_id/value MATCHES {patientList.ToAQLMatchString()}
                                ORDER BY e/ehr_id/value ASC, h/items[at0004]/value/value ASC");
        }
        public static AQLQuery EpisodeOfCare(EpsiodeOfCareParameter parameter)
        {
            return new AQLQuery($@"SELECT p/data[at0001]/items[at0071]/value/value as Beginn, 
                                b/data[at0001]/items[at0011,'Datum/Uhrzeit der Entlassung']/value/value as Ende 
                                FROM EHR e 
                                CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.fall.v1] 
                                CONTAINS (ADMIN_ENTRY p[openEHR-EHR-ADMIN_ENTRY.admission.v0] 
                                and ADMIN_ENTRY b[openEHR-EHR-ADMIN_ENTRY.discharge_summary.v0]) 
                                WHERE c/name/value = 'Stationärer Versorgungsfall' 
                                and e/ehr_id/value = '{ parameter.PatientID }' 
                                and c/context/other_context[at0001]/items[at0003,'Fall-Kennung']/value/value = '{ parameter.CaseID }'");
        }
        /*public static AQLQuery PatientLaborData(PatientListParameter patientList)
        {
            return new AQLQuery($"SELECT c/context/other_context[at0001]/items[at0002]/value/value as LabordatenID, e/ehr_id/value as PatientID, f/items[at0001]/value/value as FallID, z/items[at0001]/value/id as ProbeID, z/items[at0015]/value/value as ZeitpunktProbenentnahme, z/items[at0034]/value/value as ZeitpunktProbeneingang, z/items[at0029]/value/defining_code/code_string as MaterialID, z/items[at0029]/value/value as Material_l, g/items[at0001]/value/value as Befund, g/items[at0003]/value/value as Befundkommentar, g/items[at0024]/value/defining_code/code_string as KeimID, c/context/start_time/value as Befunddatum FROM EHR e CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.report-result.v1] CONTAINS (CLUSTER f[openEHR-EHR-CLUSTER.case_identification.v0] and CLUSTER z[openEHR-EHR-CLUSTER.specimen.v1] and CLUSTER j[openEHR-EHR-CLUSTER.laboratory_test_panel.v0] CONTAINS CLUSTER g[openEHR-EHR-CLUSTER.laboratory_test_analyte.v1]) WHERE c/name/value = 'Virologischer Befund' and g/items[at0001]/name = 'Nachweis' and g/items[at0024]/name = 'Virus' and e/ehr_id/value matches { patientList.ToAQLMatchString() }");
        }*/

        public static AQLQuery PatientLaborData(PatientListParameter patientList)
        {
            return new AQLQuery($@"SELECT e/ehr_id/value as PatientID,
                                    c/context/start_time/value as Befunddatum,
                                    y/items[at0001]/value/value as FallID,
                                    a/items[at0001]/value/id as LabordatenID,
                                    a/items[at0029]/value/defining_code/code_string as MaterialID,
                                    a/items[at0029]/value/value as Material_l,
                                    a/items[at0034]/value/value as ZeitpunktProbenentnahme,
                                    a/items[at0015]/value/value as ZeitpunktProbeneingang,
                                    d/items[at0024]/value/value as Keim_l,
                                    d/items[at0024]/value/defining_code/code_string as KeimID,
                                    d/items[at0001,'Nachweis']/value/value as Befund,
                                    d/items[at0001,'Nachweis']/value/defining_code/code_string as BefundCode,
                                    d/items[at0001,'Quantitatives Ergebnis']/value/magnitude as Viruslast,
                                    l/data[at0001]/events[at0002]/data[at0003]/items[at0101]/value/value as Befundkommentar
                                    FROM EHR e
                                    CONTAINS COMPOSITION c 
                                        CONTAINS (CLUSTER y[openEHR-EHR-CLUSTER.case_identification.v0] 
                                        and OBSERVATION l[openEHR-EHR-OBSERVATION.laboratory_test_result.v1]
                                            CONTAINS (CLUSTER a[openEHR-EHR-CLUSTER.specimen.v1]
                                            and CLUSTER b[openEHR-EHR-CLUSTER.laboratory_test_panel.v0]
                                                CONTAINS (CLUSTER d[openEHR-EHR-CLUSTER.laboratory_test_analyte.v1])))
                                    WHERE c/name/value = 'Virologischer Befund'
                                    AND e/ehr_id/value MATCHES { patientList.ToAQLMatchString() }");
        }
        public static AQLQuery NECPatientLaborData(string PatientID, TimespanParameter timespan)
        {
            //TODO: AQL musst zu eine MIBI AQL umgewandelt werden. 
            return new AQLQuery($"SELECT e/ehr_id/value as PatientID, z/items[at0015]/value/value as ZeitpunktProbenentnahme, z/items[at0029]/value/defining_code/code_string/value as MaterialID, g/items[at0001]/value/value as Befund, g/items[at0024]/value/defining_code/code_string as KeimID, c/context/start_time/value as Befunddatum FROM EHR e CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.report-result.v1] CONTAINS (CLUSTER f[openEHR-EHR-CLUSTER.case_identification.v0] and CLUSTER z[openEHR-EHR-CLUSTER.specimen.v1] and CLUSTER j[openEHR-EHR-CLUSTER.laboratory_test_panel.v0] CONTAINS CLUSTER g[openEHR-EHR-CLUSTER.laboratory_test_analyte.v1]) WHERE c/name/value = 'Virologischer Befund' and g/items[at0001]/name = 'Nachweis' and g/items[at0024]/name = 'Virus' and e/ehr_id/value = '{PatientID}' and z/items[at0015]/value/value >= '{timespan.Starttime}' and z/items[at0015]/value/value <= '{timespan.Endtime}'");
        }
        public static AQLQuery PatientPathogenFlag(PatientListParameter patientList)
        {
            //auskommentierte AQL ist die richtige AQL welche auch eigentlich genutzt werden sollte, aber die Daten sind in der MHH noch nicht richtig geladen
            //return new AQLQuery($"SELECT e/ehr_id/value as PatientID, p/data[at0001]/items[at0015]/value/value as TimeStamp, p/data[at0001]/items[at0005]/value/value as Flag, p/data[at0001]/items[at0012]/value/defining_code/code_string/value as Keim_k FROM EHR e CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.report.v1] CONTAINS EVALUATION p[openEHR-EHR-EVALUATION.flag_pathogen.v0] where c/archetype_details/template_id/value='Kennzeichnung Erregernachweis SARS-CoV-2' and e/ehr_id/value matches { patientList.ToAQLMatchString() } and p/data[at0001]/items[at0012]/value/defining_code/code_string = 'COV'");
            return new AQLQuery($"SELECT e/ehr_id/value as PatientID, c/context/start_time/value as DokumentationsDatum, p/data[at0001]/items[at0005]/value/value as Flag, p/data[at0001]/items[at0012]/value/defining_code/code_string as KeimID, p/data[at0001]/items[at0012]/value/value as Keim_l FROM EHR e CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.report.v1] CONTAINS EVALUATION p[openEHR-EHR-EVALUATION.flag_pathogen.v0] where c/archetype_details/template_id/value='Kennzeichnung Erregernachweis SARS-CoV-2' and e/ehr_id/value matches { patientList.ToAQLMatchString() } and p/data[at0001]/items[at0012]/value/defining_code/code_string = 'COV'");
        }
        public static AQLQuery LaborEpiCurve(DateTime date, string pathogenName)
        {
            return new AQLQuery($@"SELECT e/ehr_id/value as PatientID,
                                   i/items[at0001]/value/value as FallID,
                                   d/items[at0001]/value/defining_code/code_string as Flag,
                                   d/items[at0024]/value/defining_code/code_string as VirusCode,
                                   d/items[at0024]/value/value as Virus,
                                   m/items[at0015]/value/value as Datum
                            FROM EHR e
                            CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.report-result.v1]
                                CONTAINS (CLUSTER i[openEHR-EHR-CLUSTER.case_identification.v0] 
                                and OBSERVATION v[openEHR-EHR-OBSERVATION.laboratory_test_result.v1] 
                                    CONTAINS (CLUSTER m[openEHR-EHR-CLUSTER.specimen.v1] 
                                    and CLUSTER s[openEHR-EHR-CLUSTER.laboratory_test_panel.v0] 
                                        CONTAINS (CLUSTER d[openEHR-EHR-CLUSTER.laboratory_test_analyte.v1])))
                            WHERE c/name/value='Virologischer Befund' 
                            and d/items[at0001]/name/value='Nachweis' 
                            and d/items[at0024]/value/value like '{ pathogenName }*' 
                            and m/items[at0015]/value/value>='{ date.ToString("yyyy-MM-dd") }' and m/items[at0015]/value/value<'{ date.AddDays(1).ToString("yyyy-MM-dd") }'");
        }

        public static AQLQuery PatientDiagnosticResults(PatientListParameter patientList)
        {
            return new AQLQuery($"SELECT e/ehr_id/value as PatientID, j/data[at0001]/items[at0002]/value/defining_code/code_string as ICD_Code, j/data[at0001]/items[at0002]/value/value as Diagnose, j/data[at0001]/items[at0009,'Freitextbeschreibung']/value/value as Freitextbeschreibung, j/data[at0001]/items[at0077,'Datum des Auftretens/der Erstdiagnose']/value/value as DokumentationsDatum FROM EHR e CONTAINS COMPOSITION c CONTAINS EVALUATION j[openEHR-EHR-EVALUATION.problem_diagnosis.v1] WHERE c/archetype_details/template_id='Diagnose' and j/data[at0001]/items[at0002]/value/defining_code/code_string MATCHES {{'U07.1','U07.2','B34.2','B97.2'}} and e/ehr_id/value matches { patientList.ToAQLMatchString() }");
        }

        public static AQLQuery GetAllPatients(DateTime date) 
        {
            return new AQLQuery($"Select e/ehr_id/value as PatientID FROM EHR e CONTAINS COMPOSITION c CONTAINS ADMIN_ENTRY u[openEHR-EHR-ADMIN_ENTRY.hospitalization.v0] WHERE c/name/value='Patientenaufenthalt' and u/data[at0001]/items[at0004]/value/value = '{date.ToString("yyyy-MM-dd")}'");
        }
        public static AQLQuery CasesWithResults(PatientListParameter patientList)
        {
            return new AQLQuery($"SELECT DISTINCT e/ehr_id/value as PatientID, m/items[at0001]/value/value AS FallID, c/uid/value as UID FROM EHR e CONTAINS COMPOSITION c CONTAINS CLUSTER m[openEHR-EHR-CLUSTER.case_identification.v0] WHERE c/name/value = 'Mikrobiologischer Befund' AND e/ehr_id/value matches { patientList.ToAQLMatchString() } order by m/items[at0001]/value/value asc");
        }

        public static AQLQuery ReportMeta(CaseIDReceiveModel caseID)
        {
            return new AQLQuery($"SELECT DISTINCT e/ehr_id/value as PatientID, m/items[at0001]/value/value as FallID, c/uid/value as UID FROM EHR e CONTAINS COMPOSITION c CONTAINS CLUSTER m[openEHR-EHR-CLUSTER.case_identification.v0] WHERE e/ehr_id/value = '{ caseID.PatientID }' and m/items[at0001]/value/value = '{ caseID.FallID }' ORDER BY o/data[at0001]/events[at0002]/time/value DESC");
        }

        public static AQLQuery Requirements(MetaDataReceiveModel metaData)
        {
            return new AQLQuery($"select distinct a/protocol[at0004]/items[at0094]/items[at0106]/value/value as anforderung from EHR e contains COMPOSITION c contains (CLUSTER m[openEHR-EHR-CLUSTER.case_identification.v0] and OBSERVATION a[openEHR-EHR-OBSERVATION.laboratory_test_result.v1]) where (e/ehr_id/value='{ metaData.PatientID }' and c/uid/value = '{ metaData.UID }' and m/items[at0001]/value/value = '{ metaData.FallID }' )");
        }

        public static AQLQuery SamplesFromResult(MetaDataReceiveModel metaData)
        {
            return new AQLQuery($"SELECT b/items[at0029]/value/value as MaterialID, b/items[at0001]/value/id as LabordatenID, b/items[at0015]/value/value as ZeitpunktProbeentnahme, b/items[at0034]/value/value as ZeitpunktProbeneingang FROM EHR e CONTAINS COMPOSITION c CONTAINS (CLUSTER w[openEHR-EHR-CLUSTER.case_identification.v0] and CLUSTER b[openEHR-EHR-CLUSTER.specimen.v1] CONTAINS CLUSTER z[openEHR-EHR-CLUSTER.anatomical_location.v1]) where e/ehr_id/value = '{ metaData.PatientID }' and c/uid/value = '{ metaData.UID }' and  w/items[at0001]/value/value = '{ metaData.FallID }' order by b/items[at0015]/value/value desc");
        }

        //items[openEHR-EHR-CLUSTER.laboratory_test_analyte.v1]/items[openEHR-EHR-CLUSTER.erregerdetails.v1]/items[at0018]
        public static AQLQuery PathogensFromResult(MetaDataReceiveModel metaData, SampleReceiveModel sampleData)
        {
            return new AQLQuery($"SELECT distinct d/items[at0001]/value/value as KeimID, d/items[at0027]/value/magnitude as IsolatNo, d/items[at0024]/value/value as Befund, z/items[at0018]/value/value as MREKlasse, d/items[at0003]/value/value as Befundkommentar FROM EHR e CONTAINS COMPOSITION c CONTAINS (CLUSTER i[openEHR-EHR-CLUSTER.case_identification.v0] and OBSERVATION j[openEHR-EHR-OBSERVATION.laboratory_test_result.v1] CONTAINS (CLUSTER q[openEHR-EHR-CLUSTER.specimen.v1] and CLUSTER p[openEHR-EHR-CLUSTER.laboratory_test_panel.v0] CONTAINS CLUSTER d[openEHR-EHR-CLUSTER.laboratory_test_analyte.v1] CONTAINS CLUSTER z[openEHR-EHR-CLUSTER.erregerdetails.v1])) WHERE d/items[at0001]/name/value = 'Erregername' and d/items[at0024]/name/value='Nachweis?' and d/items[at0027]/name/value = 'Isolatnummer' and e/ehr_id/value = '{ metaData.PatientID }' and c/uid/value = '{ metaData.UID }' and i/items[at0001]/value/value = '{ metaData.FallID }' and q/items[at0001]/value/id = '{ sampleData.LabordatenID }'");
        }
        public static AQLQuery PatientSymptom_VS(PatientListParameter patientList)
        {
            return new AQLQuery($"SELECT e/ehr_id/value as PatientenID, a/data[at0190]/events[at0191]/data[at0192]/items[at0001]/value/value as NameDesSymptoms, a/data[at0190]/events[at0191]/data[at0192/items[at0151]/value/value as Lokalisation, a/ data[at0190]/events[at0191]/data[at0192]/items[at0152]/value/value as Beginn, a/ data[at0190]/events[at0191]/data[at0192]/items[at0021]/value/value as Schweregrad, a/ data[at0190]/events[at0191]/data[at0192]/items[at0161] as Rueckgang FROM EHR e CONTAINS COMPOSITION c CONTAINS OBSERVATION a[openEHR-EHR-OBSERVATION.symptom_sign.v0] WHERE c/archetype_details/template_id='Symptom' and e/ehr_id/value matches { patientList.ToAQLMatchString() }");
        }

        public static AQLQuery PatientSymptom_AS(PatientListParameter patientList)
        {
            return new AQLQuery($"SELECT e/ehr_id/value as PatientenID, a/data[at0001]/items[at0002]/value/value as AusschlussAussage, a/ data[at0001]/items[at0003]/value/value as Diagnose FROM EHR e CONTAINS COMPOSITION c CONTAINS EVALUATION a[openEHR-EHR-EVALUATION.exclusion_specific.v1] WHERE c/archetype_details/template_id='Symptom' and e/ehr_id/value matches { patientList.ToAQLMatchString() }");
        }

        public static AQLQuery PatientSymptom_US(PatientListParameter patientList)
        {
            return new AQLQuery($"SELECT e/ehr_id/value as PatientenID, a/data[at0001]/items[at0002]/value/value as UnbekanntesSymptom, a/ data[at0001]/items[at0005]/value/value as AussageFehlendeInfo FROM EHR e CONTAINS COMPOSITION c CONTAINS EVALUATION a[openEHR-EHR-EVALUATION.absence.v2] WHERE c/archetype_details/template_id='Symptom' and e/ehr_id/value matches { patientList.ToAQLMatchString() }");
        }
        //untested
        public static AQLQuery AntibiogramFromPathogen(MetaDataReceiveModel metaData, SampleReceiveModel sampleData, PathogenReceiveModel pathogenData)
        {
            return new AQLQuery($"SELECT w/feeder_audit/originating_system_audit/time/value as erregerZeit, b/items[at0024]/value/value as antibiotikum, b/items[at0004]/value/defining_code/code_string as resistenz, b/items[at0001]/value/magnitude as mhkMagnitude, b/items[at0001]/value/magnitude_status as mhkMagnitudeStatus, b/items[at0001]/value/units as mhkUnits, u/feeder_audit/originating_system_audit/time/value as antibiogrammZeit, b/feeder_audit/original_content/value as original FROM EHR e CONTAINS COMPOSITION c contains (CLUSTER m[openEHR-EHR-CLUSTER.case_identification.v0] and OBSERVATION j[openEHR-EHR-OBSERVATION.laboratory_test_result.v1] CONTAINS CLUSTER w[openEHR-EHR-CLUSTER.laboratory_test_analyte.v1] CONTAINS CLUSTER z[openEHR-EHR-CLUSTER.erregerdetails.v1] CONTAINS CLUSTER u[openEHR-EHR-CLUSTER.laboratory_test_panel.v0] CONTAINS CLUSTER b[openEHR-EHR-CLUSTER.laboratory_test_analyte.v1]) where e/ehr_id/value = '{ metaData.PatientID }' and c/uid/value = '{ metaData.UID }' and  w/items[at0001]/value/value = '{ metaData.FallID }' and erregerName = '{ pathogenData.KeimID }' and isolatNummer = '{ pathogenData.IsolatNo }' and w/items[at0001]/name='Erregername' and b/items[at0024]/name='Antibiotikum' order by b/items[at0024]/value/value asc");
        }

        public static AQLQuery PatientLocation(DateTime date, string patientID)
        {
            return new AQLQuery($@"SELECT a/items[at0027]/value/value as Ward, o/items[at0024]/value/defining_code/code_string as Departement 
                                FROM EHR e
                                CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.event_summary.v0] 
                                CONTAINS ADMIN_ENTRY u[openEHR-EHR-ADMIN_ENTRY.hospitalization.v0] 
                                CONTAINS (CLUSTER a[openEHR-EHR-CLUSTER.location.v1]
                                and CLUSTER o[openEHR-EHR-CLUSTER.organization.v0])
                                WHERE c/name/value = 'Patientenaufenthalt' and 
                                e/ehr_id/value = '{ patientID }' and 
                                u/data[at0001]/items[at0004]/value/value <= '{ date.ToString("O") }' and 
                                (u/data[at0001]/items[at0005]/value/value >= '{ date.ToString("O") }' or NOT EXISTS u/data[at0001]/items[at0005]/value/value)
                                ORDER BY u/data[at0001]/items[at0004]/value/value ASC");
        }

        public static AQLQuery Stationary(string patientId, DateTime datum)
        {
            return new AQLQuery($"SELECT e/ehr_id/value as PatientID, c/context/other_context[at0001]/items[at0003]/value/value  as FallID, g/data[at0001]/items[at0071]/value/value  as Datum_Uhrzeit_der_Aufnahme, z/data[at0001]/items[at0011]/value/value as Datum_Uhrzeit_der_Entlassung FROM EHR e CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.fall.v0] CONTAINS (ADMIN_ENTRY g[openEHR-EHR-ADMIN_ENTRY.admission.v0] and ADMIN_ENTRY z[openEHR-EHR-ADMIN_ENTRY.discharge_summary.v0]) WHERE c/name/value='Stationärer Versorgungsfall' and e/ehr_id/value='{patientId}' and  g/data[at0001]/items[at0071]/value/value < '{datum.Date.ToString("yyyy-MM-dd")}'");
        }

        public static AQLQuery Count(string nachweis)
        {
            return new AQLQuery($"SELECT e/ehr_id/value as PatientID, p/items[at0015]/value/value as Zeitpunkt_der_Probenentnahme FROM EHR e CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.report-result.v1] CONTAINS (CLUSTER x[openEHR-EHR-CLUSTER.laboratory_test_analyte.v1] and CLUSTER p[openEHR-EHR-CLUSTER.specimen.v1]) WHERE c/name/value='Virologischer Befund' and x/items[at0001,'Nachweis']/value/value='{nachweis}' and x/items[at0024,'Virus']/value/value='SARS-Cov-2'");
        }

        //Wenn die Fallkennung vorhanden ist
        //public static AQLQuery Stationary( string patientId, DateTime datum, string fallkennung)
        //{
        //    return new AQLQuery($"SELECT e/ehr_id/value as PatientID, c/context/other_context[at0001]/items[at0003]/value/value  as Fallkennung, g/data[at0001]/items[at0071]/value/value  as Datum_Uhrzeit_der_Aufnahme, z/data[at0001]/items[at0011]/value/value as Datum_Uhrzeit_der_Entlassung FROM EHR e CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.fall.v0] CONTAINS (ADMIN_ENTRY g[openEHR-EHR-ADMIN_ENTRY.admission.v0] and ADMIN_ENTRY z[openEHR-EHR-ADMIN_ENTRY.discharge_summary.v0]) WHERE c/name/value='Stationärer Versorgungsfall' and e/ehr_id/value='{patientId}' and  g/data[at0001]/items[at0071]/value/value < '{datum.Date.ToString("yyyy-MM-dd")}' and and c/context/other_context[at0001]/items[at0003]/value/value='{fallkennung}'");
        //}

        //public static AQLQuery Count(string nachweis)
        //{
        //    return new AQLQuery($"SELECT e/ehr_id/value as PatientID, p/items[at0015]/value/value as Zeitpunkt_der_Probenentnahme, s/items[at0001]/value/value as Fallkennung FROM EHR e CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.report-result.v1] CONTAINS (CLUSTER x[openEHR-EHR-CLUSTER.laboratory_test_analyte.v1] and CLUSTER p[openEHR-EHR-CLUSTER.specimen.v1] and CLUSTER s[openEHR-EHR-CLUSTER.case_identification.v0]) WHERE c/name/value='Virologischer Befund' and x/items[at0001,'Nachweis']/value/value='{nachweis}' and x/items[at0024,'Virus']/value/value='SARS-Cov-2'");
        //}

        public static AQLQuery Case(DateTime date)
        {
            return new AQLQuery($"SELECT COUNT(DISTINCT e/ehr_id/value) as Anzahl_Faelle FROM EHR e CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.report-result.v1] CONTAINS (CLUSTER x[openEHR-EHR-CLUSTER.laboratory_test_analyte.v1] and CLUSTER p[openEHR-EHR-CLUSTER.specimen.v1]) WHERE c/name/value='Virologischer Befund' and x/items[at0001,'Nachweis']/value/value='positiv' and x/items[at0024,'Virus']/value/value='SARS-Cov-2' and p/items[at0015]/value/value = '{date.Date.ToString("yyyy-MM-dd")}'");
        }

        public static AQLQuery WeekCase(DateTime startDate, DateTime endDate)
        {
            return new AQLQuery($"SELECT COUNT(DISTINCT e/ehr_id/value) as Anzahl_Faelle FROM EHR e CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.report-result.v1] CONTAINS (CLUSTER x[openEHR-EHR-CLUSTER.laboratory_test_analyte.v1] and CLUSTER p[openEHR-EHR-CLUSTER.specimen.v1]) WHERE c/name/value='Virologischer Befund' and x/items[at0001,'Nachweis']/value/value='positiv' and x/items[at0024,'Virus']/value/value='SARS-Cov-2' and p/items[at0015]/value/value >= '{startDate.Date.ToString("yyyy-MM-dd")}' and p/items[at0015]/value/value <= '{endDate.Date.ToString("yyyy-MM-dd")}' ");
        }
    }
}
 