﻿using System.Diagnostics;
using System.Text.Json;
using MHServerEmu.Common.Logging;
using MHServerEmu.Games.GameData.Calligraphy;
using MHServerEmu.Games.GameData.LiveTuning;
using MHServerEmu.Games.Properties;

namespace MHServerEmu.Games.GameData
{
    public static class GameDatabase
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        private static readonly string PakDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "GPAK");
        private static readonly string CalligraphyPath = Path.Combine(PakDirectory, "Calligraphy.sip");
        private static readonly string ResourcePath = Path.Combine(PakDirectory, "mu_cdata.sip");

        public static bool IsInitialized { get; }

        public static DataDirectory DataDirectory { get; private set; }
        public static PropertyInfoTable PropertyInfoTable { get; private set; }
        public static List<LiveTuningSetting> LiveTuningSettingList { get; private set; }

        // DataRef is a unique ulong id that may change across different versions of the game (e.g. resource DataRef is hashed file path).
        public static DataRefManager<StringId> StringRefManager { get; } = new(false);
        public static DataRefManager<AssetTypeId> AssetTypeRefManager { get; } = new(true);
        public static DataRefManager<CurveId> CurveRefManager { get; } = new(true);
        public static DataRefManager<BlueprintId> BlueprintRefManager { get; } = new(true);
        public static DataRefManager<PrototypeId> PrototypeRefManager { get; } = new(true);

        static GameDatabase()
        {
            // Make sure sip files are present
            if (File.Exists(CalligraphyPath) == false || File.Exists(ResourcePath) == false)
            {
                Logger.Fatal($"Calligraphy.sip and/or mu_cdata.sip are missing! Make sure you copied these files to {PakDirectory}.");
                IsInitialized = false;
                return;
            }

            Logger.Info("Initializing game database...");
            var stopwatch = Stopwatch.StartNew();

            // Initialize DataDirectory
            DataDirectory = new(new PakFile(CalligraphyPath), new PakFile(ResourcePath));

            // Initialize PropertyInfoTable
            PropertyInfoTable = new(DataDirectory);

            // Load live tuning
            LiveTuningSettingList = JsonSerializer.Deserialize<List<LiveTuningSetting>>(File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Assets", "LiveTuning.json")));
            Logger.Info($"Loaded {LiveTuningSettingList.Count} live tuning settings");

            // Verify
            if (VerifyData() == false)
            {
                Logger.Fatal("Failed to initialize game database");
                IsInitialized = false;
                return;
            }

            // Finish game database initialization
            stopwatch.Stop();
            Logger.Info($"Finished initializing game database in {stopwatch.ElapsedMilliseconds} ms");
            IsInitialized = true;
        }

        #region Data Access

        public static AssetType GetAssetType(AssetTypeId assetTypeId) => DataDirectory.AssetDirectory.GetAssetType(assetTypeId);
        public static Curve GetCurve(CurveId curveId) => DataDirectory.CurveDirectory.GetCurve(curveId);
        public static Blueprint GetBlueprint(BlueprintId blueprintId) => DataDirectory.GetBlueprint(blueprintId);
        public static T GetPrototype<T>(PrototypeId prototypeId) => DataDirectory.GetPrototype<T>(prototypeId);

        public static string GetAssetName(StringId assetId) => StringRefManager.GetReferenceName(assetId);
        public static string GetAssetTypeName(AssetTypeId assetTypeId) => AssetTypeRefManager.GetReferenceName(assetTypeId);
        public static string GetCurveName(CurveId curveId) => CurveRefManager.GetReferenceName(curveId);
        public static string GetBlueprintName(BlueprintId blueprintId) => BlueprintRefManager.GetReferenceName(blueprintId);
        public static string GetBlueprintFieldName(StringId fieldId) => StringRefManager.GetReferenceName(fieldId);
        public static string GetPrototypeName(PrototypeId prototypeId) => PrototypeRefManager.GetReferenceName(prototypeId);

        public static PrototypeId GetDataRefByPrototypeGuid(PrototypeGuid guid) => DataDirectory.GetPrototypeDataRefByGuid(guid);

        // Our implementation of GetPrototypeRefByName combines both GetPrototypeRefByName and GetDataRefByResourceGuid.
        // The so-called "ResourceGuid" is actually just a prototype name, and in the client both of these methods work
        // by rehashing the file path on each call to get an id, with GetPrototypeRefByName working only with Calligraphy
        // prototypes, and GetDataRefByResourceGuid working only with resource prototypes (because Calligraphy and resource
        // prototypes have different pre-hashing steps, see HashHelper for more info).
        //
        // We avoid all of this additional complexity by simply using a reverse lookup dictionary in our PrototypeRefManager.
        public static PrototypeId GetPrototypeRefByName(string name) => PrototypeRefManager.GetDataRefByName(name);

        public static PrototypeGuid GetPrototypeGuid(PrototypeId id) => DataDirectory.GetPrototypeGuid(id);

        public static PrototypeId GetDataRefByAsset(StringId assetId)
        {
            if (assetId == StringId.Invalid) return PrototypeId.Invalid;

            string assetName = GetAssetName(assetId);
            return GetPrototypeRefByName(assetName);
        }

        #endregion

        private static bool VerifyData()
        {
            return DataDirectory.Verify()
                && PropertyInfoTable.Verify();
        }
    }
}
