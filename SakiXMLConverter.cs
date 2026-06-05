using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Virinco.WATS.Interface;

namespace Virinco.WATS.Converter.Saki
{
    public class SakiXMLConverter : IReportConverter_v2
    {
        Dictionary<string, string> parameters;
        public SakiXMLConverter() : base()
        {
            parameters = new Dictionary<string, string>()
            {
                {"operationTypeCode","10" },
                {"sequenceName","SoftwareName" },
                {"sequenceVersion","1.0.0" }
            };
        }

        public Dictionary<string, string> ConverterParameters => parameters;

        public SakiXMLConverter(Dictionary<string, string> args)
        {
            parameters = args;
        }

        public void CleanUp()
        {
        }


        public Report ImportReport(TDM api, Stream file)
        {

            using (StreamReader reader = new StreamReader(file))
            {
                XDocument xmlReport = XDocument.Load(reader);
                Report WATSReport = ReadReport(xmlReport, api);
                return WATSReport;
            }
        }



        private StepStatusType GetStepStatusType(string status)
        {
            if (status == "failed")
            {
                return StepStatusType.Failed;
            }
            else
            {
                return StepStatusType.Passed;
            }
        }

        private UUTStatusType GetUUTStatusType(string status)
        {
            if (status == "failed")
            {
                return UUTStatusType.Failed;
            }
            else
            {
                return UUTStatusType.Passed;
            }
        }

        private Report ReadReport(XDocument xmlReportDoc, TDM api)
        {

            api.TestMode = TestModeType.Active;

            XElement xmlPanel = xmlReportDoc.Element("Panel");
            XElement xmlProgram = xmlPanel.Element("Program");

            UUTReport uut = api.CreateUUTReport(
                            xmlPanel.Attribute("operator").Value,
                            xmlPanel.Attribute("side").Value,
                            "",
                            xmlPanel.Attribute("barcode").Value,
                            ConverterParameters["operationTypeCode"],
                            xmlProgram.Attribute("name").Value,
                            xmlProgram.Attribute("ver").Value);


            string startDateString = xmlPanel.Attribute("startDate").Value + " " + xmlPanel.Attribute("startTime").Value;
            string format = "MM/dd/yyyy HH:mm:ss";
            CultureInfo provider = CultureInfo.InvariantCulture;
            DateTime parsedStartDate = DateTime.ParseExact(startDateString, format, provider);
            uut.StartDateTime = parsedStartDate;

            var images = xmlPanel.Elements("Image");

            SequenceCall currentSequence = null;
            SequenceCall falseCallsSequence = null;
            SequenceCall failedSequence = null;

            foreach (var image in images)
            {
                XElement xmlResults = image.Element("Results");

                XElement xmlFalseCalls = xmlResults.Element("FalseCalls");


                XElement xmlFailed = xmlResults.Element("Failed");


                string sequenceName = "Image " + image.Attribute("id").Value;
                currentSequence = uut.GetRootSequenceCall().AddSequenceCall(sequenceName);


                if (xmlFalseCalls != null)
                {
                    var falseCallsRefrences = xmlFalseCalls.Elements("Reference");
                    if (falseCallsRefrences.Count() > 0)
                    {
                        falseCallsSequence = currentSequence.AddSequenceCall("False Calls");

                        foreach (var refrence in falseCallsRefrences)
                        {
                            StringValueStep currentStep = falseCallsSequence.AddStringValueStep(refrence.Attribute("designator").Value);
                            currentStep.Status = StepStatusType.Passed;
                        }

                        falseCallsSequence.Status = StepStatusType.Passed;
                    }

                }

                if (xmlFailed != null)
                {
                    var failedRefrences = xmlFailed.Elements("Reference");
                    if (failedRefrences.Count() > 0)
                    {
                        failedSequence = currentSequence.AddSequenceCall("Failed");

                        foreach (var refrence in failedRefrences)
                        {
                            StringValueStep currentStep = failedSequence.AddStringValueStep(refrence.Attribute("designator").Value);
                            currentStep.Status = StepStatusType.Failed;
                        }

                        failedSequence.Status = StepStatusType.Failed;
                    }
                }

                string sequenceStatus = image.Attribute("result").Value;
                currentSequence.Status = (StepStatusType)GetStepStatusType(sequenceStatus);

            }

            var uutStatusString = xmlPanel.Attribute("result").Value;

            uut.Status = GetUUTStatusType(uutStatusString);

            return uut;
        }
    }

}
