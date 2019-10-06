using Eleon.Modding;
using EmpyrionNetAPIAccess;
using EmpyrionNetAPITools;
using EmpyrionNetAPIDefinitions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EmpyrionNetAPITools.Extensions;
using System.Numerics;

namespace EmpyrionPoiRestore
{
    public class PoiRestore : EmpyrionModBase
    {
        public ModGameAPI DediAPI { get; set; }
        public ConfigurationManager<Configuration> Configuration { get; set; }
        public DateTime LastGSLTimestamp { get; set; } = DateTime.Now;

        public PoiRestore()
        {
            EmpyrionConfiguration.ModName = "EmpyrionPoiRestore";
        }
        public override void Initialize(ModGameAPI dediAPI)
        {
            DediAPI = dediAPI;

            try
            {
                Log($"**EmpyrionPoiRestore loaded: {string.Join(" ", Environment.GetCommandLineArgs())}", LogLevel.Message);

                LoadConfiguration();
                LogLevel = Configuration.Current.LogLevel;
                ChatCommandManager.CommandPrefix = Configuration.Current.ChatCommandPrefix;

                ChatCommands.Add(new ChatCommand(@"poipos help", (I, A) => DisplayHelp(I.playerId, null), "display help"));
                ChatCommands.Add(new ChatCommand(@"poipos add (?<Name>.+)", (I, A) => StorePoiPosAndRot(I.playerId, A), "store POI pos and rot", PermissionType.Admin));

                TaskTools.Intervall(Configuration.Current.CheckPoiPositionsEveryNSeconds * 1000, async () => await CheckPlayfields());
                Event_Playfield_Loaded += P => TaskTools.Delay(Configuration.Current.CheckPoiPositionsNSecondsAfterPlayfieldLoaded, async () => await CheckPlayfields());
            }
            catch (Exception Error)
            {
                Log($"**EmpyrionPoiRestore Error: {Error} {string.Join(" ", Environment.GetCommandLineArgs())}", LogLevel.Error);
            }

        }

        private async Task CheckPlayfields()
        {
            try
            {
                var playfields = (await Request_Playfield_List()).playfields;
                GlobalStructureList GSL = null;

                playfields
                    .Where(P => Configuration.Current.PoiData.ContainsKey(P))
                    .ToList()
                    .ForEach(async P => {
                        CheckPoiPositions(P, GSL ?? (GSL = await Request_GlobalStructure_List()));
                    });
            }
            catch (Exception error)
            {
                Log($"**EmpyrionPoiRestore CheckPlayfields Error: {error}", LogLevel.Error);
            }
        }

        private async Task StorePoiPosAndRot(int playerId, Dictionary<string, string> args)
        {
            var P = await Request_Player_Info(playerId.ToId());

            if (!(await Request_GlobalStructure_List()).globalStructures.TryGetValue(P.playfield, out var gsi)) return;

            var poiList = Configuration.Current.PoiData.AddOrUpdate(P.playfield, N => new List<PoiData>(), (N, S) => S);
            var data = poiList.FirstOrDefault(L => L.Name == args["Name"]);
            if (data == null)
            {
                data = new PoiData() { Name = args["Name"] };
                poiList.Add(data);
            }

            data.Positions = gsi.Where(G => G.name == args["Name"]).Select(G => new PoiPosition() { Pos = Vector(G.pos), Rot = Vector(G.rot) }).ToList();

            Configuration.Save();
        }

        private void CheckPoiPositions(string playfield, GlobalStructureList gsl)
        {
            if (!Configuration.Current.PoiData  .TryGetValue(playfield, out var pois)) return;
            if (!gsl.globalStructures           .TryGetValue(playfield, out var gsi))  return;

            bool saveConfig = false;
            pois.ForEach(S => {
                var currentPositions = gsi.Where(G => G.name == S.Name).Select(G => new { G, Pos = Vector(G.pos), Rot = Vector(G.rot) }).ToList();
                if (S.Positions == null)
                {
                    S.Positions = currentPositions.Select(G => new PoiPosition() { Pos = G.Pos, Rot = G.Rot }).ToList();
                    saveConfig = true;
                }
                else
                {
                    var changePos = new List<GlobalStructureInfo>();
                    var checkPos = S.Positions.ToList();
                    currentPositions.ForEach(G =>
                    {
                        var found = checkPos.FirstOrDefault(C => C.Pos == G.Pos && C.Rot == G.Rot);
                        if (found != null) checkPos.Remove(found);
                        else changePos.Add(G.G);
                    });

                    changePos.ForEach(
                        G =>
                        {
                            var setPos = checkPos.FirstOrDefault();
                            if (setPos != null)
                            {
                                checkPos.Remove(setPos);
                                try
                                {
                                    Request_Entity_Teleport(new IdPositionRotation(G.id,
                                        new PVector3(setPos.Pos.X, setPos.Pos.Y, setPos.Pos.Z),
                                        new PVector3(setPos.Rot.X, setPos.Rot.Y, setPos.Rot.Z)))
                                    .Wait(2000);
                                }
                                catch (Exception error)
                                {
                                    Log($"EntityMove failed: {G.id} '{G.name}' Pos:{setPos.Pos} Rot:{setPos.Rot} => {error}", LogLevel.Debug);
                                }
                            }
                        });
                }
            });

            if (saveConfig) Configuration.Save();
        }

        private Vector3 Vector(PVector3 v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        private void LoadConfiguration()
        {
            ConfigurationManager<Configuration>.Log = Log;
            Configuration = new ConfigurationManager<Configuration>() { ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, "Configuration.json") };

            Configuration.Load();
            Configuration.Save();
        }
    }
}
