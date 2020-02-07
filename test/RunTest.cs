using Xunit;
using Hypar.Functions.Execution;
using System.Threading.Tasks;
using Elements;
using Elements.Geometry;
using Xunit.Abstractions;
using Hypar.Functions.Execution.Local;
using System.Collections.Generic;
using System.IO;

namespace SubdivideSlab.Tests
{
    public class RunTest
    {
        [Fact]
        public void RunSubdivAlgo()
        {
            var jsonIn = System.IO.File.ReadAllText("../../../../Floors.json");
            var model = Model.FromJson(jsonIn);
            var inputs = new SubdivideSlabInputs(2, 2, false, "", "", new Dictionary<string, string>(), "", "", "");
            var outputs = SubdivideSlab.Execute(new Dictionary<string, Model> { { "Floors", model } }, inputs);
            var json = outputs.model.ToJson();

        }
    }
}