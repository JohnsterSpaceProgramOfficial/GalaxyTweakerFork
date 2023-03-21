﻿using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SpaceWarp;
using SpaceWarp.API.Mods;
using UnityEngine;
using KSP.Game.Load;
using KSP.Game;
using System.IO;
using System;
using System.Collections.Generic;
using KSP.UI.Binding;
using SpaceWarp.API.UI;


namespace GalaxyTweaker
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
    public class GalaxyTweakerPlugin : BaseSpaceWarpPlugin
    {
        // These are useful in case some other mod wants to add a dependency to this one
        public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
        public const string ModName = MyPluginInfo.PLUGIN_NAME;
        public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

        public static string Path { get; private set; }

        private static ConfigEntry<string> _selectedTarget;

        private static string DefaultDirectory => $"{Path}/GalaxyDefinitions";
        private static string DefaultPath => $"{Path}/GalaxyDefinitions/GalaxyDefinition_Default.json";
        private static string ConfigPath => $"{Path}/GalaxyDefinitions/{_selectedTarget.Value}.json";
        private static string CampaignDirectory => $"{Path}/saves/{Game.SessionManager.ActiveCampaignName}";
        private static string CampaignPath => $"{CampaignDirectory}/CampaignGalaxyDefinition.json";

        public static GalaxyTweakerPlugin Instance { get; set; }
        private static ManualLogSource _logger;

        public override void OnPreInitialized()
        {
            base.OnPreInitialized();
            Path = PluginFolderPath;
            Instance = this;
            _logger = Logger;
        }

        public override void OnInitialized()
        {
            // Register all Harmony patches in the project
            Harmony.CreateAndPatchAll(typeof(GalaxyTweakerPlugin));

            // Generate GalaxyDefinition_Default.json from address
            if (!File.Exists(DefaultPath))
            {
                Directory.CreateDirectory($"{Path}/GalaxyDefinitions");
                GameManager.Instance.Game.Assets.Load<TextAsset>("GalaxyDefinition_Default", asset =>
                    File.WriteAllText(DefaultPath, asset.text)
                );
                _logger.LogInfo($"Copying the original asset into: {DefaultPath}");
            }

            // Fetch a configuration value or create a default one if it does not exist
            _selectedTarget = Config.Bind(
                "Galaxy Definition Selection",
                "Selected Galaxy Definition",
                "GalaxyDefinition_Default",
                "This is the file that the game will load when creating a new campaign. Inside <KSP2 Install>/BepInEx/plugins/galaxy_tweaker/GalaxyDefinitions is where these files are stored. GalaxyDefinition_Default represents the vanilla KSP2 galaxy, and is automatically regenerated on game load. To load in a custom galaxy definition, copy the default, tweak the values as you wish, and go back here and type in the file name (WITHOUT THE .json), and when you create a new campaign it should automatically load in celestial bodies as you have put them in in the file. If your game is getting stuck at \"Loading Celestial Body Data\" then either the file or this input is wrong!"
            );
            _logger.LogInfo($"Found config value: {_selectedTarget.Value}");

            windowOpen = true;
            galaxyDefsList.Clear();
            GetGalaxyDefinitions();
        }

        [HarmonyPatch(typeof(LoadCelestialBodyDataFilesFlowAction), nameof(LoadCelestialBodyDataFilesFlowAction.DoAction))]
        [HarmonyPrefix]
        public static bool LoadCelestialBodyDataFilesFlowAction_DoAction(Action resolve, LoadCelestialBodyDataFilesFlowAction __instance)
        {
            __instance._game.UI.SetLoadingBarText(__instance.Description);
            __instance._resolve = resolve;

            LoadDefinitions(__instance.OnGalaxyDefinitionLoaded);

            return false;
        }

        [HarmonyPatch(typeof(CreateCelestialBodiesFlowAction), nameof(CreateCelestialBodiesFlowAction.DoAction))]
        [HarmonyPrefix]
        public static bool CreateCelestialBodiesFlowAction_DoAction(Action resolve, CreateCelestialBodiesFlowAction __instance)
        {
            __instance._game.UI.SetLoadingBarText(__instance.Description);
            __instance._resolve = resolve;

            LoadDefinitions(__instance.OnGalaxyDefinitionLoaded);

            return false;
        }

        private static void LoadDefinitions(Action<TextAsset> onGalaxyDefinitionLoaded)
        {
            _logger.LogInfo("File Exists: " + File.Exists(CampaignPath).ToString());
            _logger.LogInfo("Campaign Exists: " + Game.SaveLoadManager.CampaignExists(Game.SessionManager.ActiveCampaignType, Game.SessionManager.ActiveCampaignName).ToString());

            if (Game.SaveLoadManager.CampaignExists(Game.SessionManager.ActiveCampaignType, Game.SessionManager.ActiveCampaignName) && !File.Exists(CampaignPath))
            {
                _logger.LogInfo("USING DEFAULT GALAXYDEFINITION!");
                GameManager.Instance.Game.Assets.Load<TextAsset>("GalaxyDefinition_Default", asset =>
                    onGalaxyDefinitionLoaded(new TextAsset(asset.text))
                );
                _logger.LogInfo($"Loaded default campaign definition.");
                return;
            }

            
            _logger.LogInfo("Did not return out of default exception. Performing normally.");
            if (!File.Exists(CampaignPath))
            {
                Directory.CreateDirectory(CampaignDirectory);
                File.WriteAllText(CampaignPath, File.ReadAllText(ConfigPath));
                _logger.LogInfo($"Campaign definition not found, creating file: {CampaignPath}");
            }

            var jsonFeed = File.ReadAllText(CampaignPath);
            _logger.LogInfo($"Loaded campaign definition: {CampaignPath}");
            onGalaxyDefinitionLoaded(new TextAsset(jsonFeed));
        }




        //Toggles the visibility of the UI window
        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                windowOpen = !windowOpen;
            }
        }

        //Draws a simple UI window when the [windowOpen] boolean is set to [true].
        void OnGUI()
        {
            if (windowOpen)
            {
                GUI.skin = Skins.ConsoleSkin;
                windowRect = GUILayout.Window(
                    GUIUtility.GetControlID(FocusType.Passive),
                    windowRect,
                    WindowContent,
                    "// Galaxy Tweaker UI Concept",
                    GUILayout.Width(500)
                );
            }
        }

        //Defines the content of the UI window drawn in the [OnGUI] method.
        private void WindowContent(int windowID)
        {
            GUILayout.Label("<size=25>GALAXY SETUP</size>");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Selected Galaxy Definition: ");
            galaxyDefinition = GUILayout.TextField(galaxyDefinition, 45);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Load Selected Galaxy Definition"))
            {
                if (File.Exists(galaxyDefFolderLoc + galaxyDefinition))
                {
                    loadedFilePath = galaxyDefFolderLoc + galaxyDefinition;
                    Debug.Log("The location of the specified galaxy definition file was valid!");
                }
                else
                {
                    Debug.Log("No galaxy defintion file was found at that path!");
                    if (galaxyDefsList.Count > 0)
                    {
                        galaxyDefinition = galaxyDefsList[0];
                    }
                }
            }
            if (GUILayout.Button("Reload Galaxy Definitions"))
            {
                galaxyDefsList.Clear();
                GetGalaxyDefinitions();
            }
            GUILayout.Space(20);
            if (galaxyDefsList.Count == 1)
            {
                GUILayout.Label("<size=20>" + galaxyDefsList.Count + " Galaxy Definition Was Found!</size>");
            }
            else
            {
                GUILayout.Label("<size=20>" + galaxyDefsList.Count + " Galaxy Definitions Were Found!</size>");
            }
            if (galaxyDefsList.Count > 4)
            {
                GUILayout.BeginVertical();
                scrollbarPos = GUILayout.BeginScrollView(scrollbarPos, false, true, GUILayout.Height(100));
            }
            foreach (string galaxyDef in galaxyDefsList)
            {
                if (GUILayout.Button(galaxyDef))
                {
                    galaxyDefinition = galaxyDef;
                }
            }
            if (galaxyDefsList.Count > 4)
            {
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
            GUI.DragWindow(new Rect(0, 0, 10000, 10000));
        }

        private void GetGalaxyDefinitions()
        {
            DirectoryInfo galaxyDefFolder = new DirectoryInfo(galaxyDefFolderLoc);
            FileInfo[] galaxyDefInfo = galaxyDefFolder.GetFiles("*" + galaxyDefFileType + "*");
            foreach (FileInfo galaxyDef in galaxyDefInfo)
            {
                if (!galaxyDefsList.Contains(galaxyDef.Name))
                {
                    galaxyDefsList.Add(galaxyDef.Name);
                }
            }
        }

        [SerializeField]
        private bool windowOpen;
        private Rect windowRect;

        [SerializeField]
        private string galaxyDefinition = "GalaxyDefinition_Default.json";
        [SerializeField]
        private string galaxyDefFileType = ".json";

        [SerializeField]
        private string loadedFilePath;
        private string galaxyDefFolderLoc = "C:/Program Files (x86)/Steam/steamapps/common/Kerbal Space Program 2/BepInEx/plugins/galaxy_tweaker/GalaxyDefinitions/";
        [SerializeField]
        private List<string> galaxyDefsList;

        [SerializeField]
        private Vector2 scrollbarPos;








    }
}