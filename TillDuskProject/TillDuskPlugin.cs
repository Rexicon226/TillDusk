﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using KSP.Game;
using KSP.Messages;
using KSP.Sim;
using KSP.Sim.impl;
using KSP.UI.Binding;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.Game;
using SpaceWarp.API.Game.Extensions;
using SpaceWarp.API.UI;
using SpaceWarp.API.UI.Appbar;
using UnityEngine;
using UnityEngine.Serialization;

namespace TillDusk;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class TillDuskPlugin : BaseSpaceWarpPlugin
{
    // These are useful in case some other mod wants to add a dependency to this one
    public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    public const string ModName = MyPluginInfo.PLUGIN_NAME;
    public const string ModVer = MyPluginInfo.PLUGIN_VERSION;
    
    private static ConfigEntry<int> _bestSunriseTime;
    private static ConfigEntry<int>  _utRounding;
    private static ConfigEntry<int> _timePadding;
    private static ConfigEntry<int> _sunriseWarpIndex;

    private static bool isEdgeWarping;

    private static bool _isWindowOpen;
    private Rect _windowRect;

    private const string ToolbarFlightButtonID = "BTN-TillDuskFlight";
    private const string ToolbarOABButtonID = "BTN-TillDuskOAB";

    public static TillDuskPlugin Instance { get; set; }
    
    public override void OnInitialized()
    {
        base.OnInitialized();

        Instance = this;
        
        Appbar.RegisterAppButton(
            "Till Dusk",
            ToolbarFlightButtonID,
            AssetManager.GetAsset<Texture2D>($"{SpaceWarpMetadata.ModID}/images/icon.png"),
            isOpen =>
            {
                _isWindowOpen = isOpen;
                GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
            }
        );
        
        Harmony.CreateAndPatchAll(typeof(TillDuskPlugin).Assembly);
        
        _bestSunriseTime = Config.Bind<int>("Settings section", "Sunrise Time", 15420, "The time into the day at which the best sunrise will occur. (In seconds)");
        _utRounding = Config.Bind<int>("Settings section", "UT Decimals", 0, "The amount of decimal points the UT should have in the window.");
        _timePadding = Config.Bind<int>("Settings section", "Time Padding", 200, "The amount of time to pad the warp to sunrise by.");
        _sunriseWarpIndex = Config.Bind<int>("Settings section", "Sunrise Warp Index", 7, "How fast the warp to sunrise should be. 1-10");
    }
    
    private void OnGUI()
    {
        // Set the UI
        GUI.skin = Skins.ConsoleSkin;

        if (_isWindowOpen)
        {
            _windowRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                _windowRect,
                FillWindow,
                "Till Dusk",
                GUILayout.Height(250),
                GUILayout.Width(350)
            );
        }
    }

    private static void CloseWindow()
    {
        _isWindowOpen = !_isWindowOpen;
        GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
    }

    private static void FillWindow(int windowID)
    {
        double UT = GameManager.Instance.Game.UniverseModel.UniversalTime;
        GUILayout.Label("The current in-game time is: " + Math.Round(UT, _utRounding.Value));
        
        var dayCount = Math.Floor(Math.Round(UT, 0) / 21600);

        var nextSunrise = dayCount * 21600 + _bestSunriseTime.Value;
        
        if (nextSunrise - UT < 0)
        {
            nextSunrise = (dayCount + 1) * 21600 + _bestSunriseTime.Value;
        }
        
        var timeManager = GameManager.Instance.Game.ViewController?.TimeWarp;
        var testInt1 = 0;
        
        GUILayout.Label("The next sunrise at: " + nextSunrise);
        if (GUILayout.Button("Warp to next sunrise"))
        {
            if (timeManager == null)
            {
                return;
            }
            if (!isEdgeWarping)
            {
                for (int i = 0; i < _sunriseWarpIndex.Value; i++)
                {
                    if (timeManager._currentRate < _sunriseWarpIndex.Value)
                    {
                        timeManager.IncreaseTimeWarp();
                    }
                }
                isEdgeWarping = true;
            }
        }

        if (isEdgeWarping)
        {
                    
            if (GUILayout.Button("Stop Warping"))
            {
                timeManager.StopTimeWarp();
                isEdgeWarping = false;
            }
            
            GUILayout.Label("Warping to next sunrise...");
            GUILayout.Label(nextSunrise - _timePadding.Value + " > " + Math.Round(UT, _utRounding.Value));
            if (UT > nextSunrise - _timePadding.Value)
            {
                GUILayout.Label("Warping to next sunrise... Done!");
                timeManager?.StopTimeWarp();
                isEdgeWarping = false;
            }
        }
        if (GUILayout.Button("Close Window"))
        {
            CloseWindow();
            GUIUtility.ExitGUI();
        }
        GUI.DragWindow(new Rect(0, 0, 10000, 500));
    }
}
