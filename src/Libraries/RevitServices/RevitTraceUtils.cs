using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Autodesk.Revit.DB;
using DynamoServices;

namespace RevitServices
{
    public class Constants
    {
        public const string RevitTraceID = "Revit-Slot-{8C34D023-EF13-43A7-A210-C7EC45731FED}";
    }

    public static class RevitTraceUtils
    {
        public static List<ElementId> GetElementIds()
        {
            SerializableListOfElements listOfElements = 
                (SerializableListOfElements)TraceUtils.GetTraceData(Constants.RevitTraceID);
            return listOfElements.ElementIds;
        }

        public static void SetElementIds(List<ElementId> elementIds)
        {
            SerializableListOfElements listOfElements = 
                new SerializableListOfElements(elementIds);

            TraceUtils.SetTraceData(Constants.RevitTraceID, listOfElements);
        }


        internal class SerializableListOfElements : ISerializable
        {
            public List<ElementId> ElementIds  { get; private set; }

            public SerializableListOfElements(List<ElementId> elementsIds)
            {
                this.ElementIds = elementsIds;
            }

            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                throw new NotImplementedException();
            }
        }

    }
}
