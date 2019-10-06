using EmpyrionNetAPIDefinitions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;

namespace EmpyrionPoiRestore
{
    public class PoiPosition
    {
        public Vector3 Pos { get; set; }
        public Vector3 Rot { get; set; }
    }
    public class PoiData
    {
        public string Name { get; set; }
        public List<PoiPosition> Positions { get; set; }
    }
    public class Configuration
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public LogLevel LogLevel { get; set; } = LogLevel.Message;
        public string ChatCommandPrefix { get; set; } = "/\\";
        public int CheckPoiPositionsEveryNSeconds { get; set; } = 60;
        public int CheckPoiPositionsNSecondsAfterPlayfieldLoaded { get; set; } = 10;
        public ConcurrentDictionary<string, List<PoiData>> PoiData { get; set; } = new ConcurrentDictionary<string, List<PoiData>>()
        {
            ["Playfieldname"] = new List<PoiData>() { new PoiData() { Name = "PoiName" } }
        };
    }
}