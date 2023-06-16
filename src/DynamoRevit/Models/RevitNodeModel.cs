using System;
using System.Collections.Generic;
using Dynamo.Graph.Nodes;
using Newtonsoft.Json;

namespace Dynamo.Applications.Models
{
    [Obsolete("This class will be removed, please use the class in RevitNodesUI")]
    public abstract class RevitNodeModel : NodeModel
    {
        public RevitNodeModel() { }

        [JsonConstructor]
        public RevitNodeModel(IEnumerable<PortModel> inPorts, IEnumerable<PortModel> outPorts) : base(inPorts, outPorts) { }
    }
}
