using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using KSP.Game;
using KSP.UI.Binding;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.UI.Appbar;
using SpaceWarp.API.UI;
using SpaceWarp.Backend.UI.Loading;
using UnityEngine;
using JetBrains.Annotations;
using SpaceWarp.API.Loading;

namespace TillDusk;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class TillDuskPlugin : BaseSpaceWarpPlugin
{
    // These are useful in case some other mod wants to add a dependency to this one
    [PublicAPI] public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    [PublicAPI] public const string ModName = MyPluginInfo.PLUGIN_NAME;
    [PublicAPI] public const string ModVer = MyPluginInfo.PLUGIN_VERSION;
    
    private static ConfigEntry<int> _bestSunriseTime;
    private static ConfigEntry<int>  _utRounding;
    private static ConfigEntry<int> _timePadding;
    private static ConfigEntry<int> _sunriseWarpIndex;
    
    private static bool _isEdgeWarping;
    private static int _dayStartedWarp;

    private static bool _isWindowOpen;
    private static Rect _windowRect;

    private const string ToolbarFlightButtonID = "BTN-TillDuskFlight";

    public override void OnInitialized()
    {
        base.OnInitialized();

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
        
        _bestSunriseTime = Config.Bind("Settings section", "Sunrise Time", 15420, "The time into the day at which the best sunrise will occur. (In seconds)");
        _utRounding = Config.Bind("Settings section", "UT Decimals", 0, "The amount of decimal points the UT should have in the window.");
        _timePadding = Config.Bind("Settings section", "Time Padding", 200, "The amount of time to pad the warp to sunrise by.");
        _sunriseWarpIndex = Config.Bind(
            "Settings section",
            "Sunrise Warp Index", 
            6,
            new ConfigDescription(
                "How fast the warp to sunrise should be. 1-10",
                new AcceptableValueRange<int>(1, 7)
        ));
    }
    
    private void Update()
    {
        if (_sunriseWarpIndex is { Value: > 6 })
        {
            _timePadding.Value = 600;
        }
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
                GUILayout.Height(125),
                GUILayout.Width(400)
            );
        }
    }

    private static void CloseWindow()
    {
        _isWindowOpen = !_isWindowOpen;
        GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
    }

    private static String Ut2String(double ut)
    {
        var days = Math.Floor(ut / 21600);
        var hours = Math.Floor((ut % 21600) / 3600);
        var minutes = Math.Floor(((ut % 21600) % 3600) / 60);
        var secondsLeft = Math.Floor(((ut % 21600) % 3600) % 60);

        var dayString = days.ToString(CultureInfo.CurrentCulture);
        var hourString = hours.ToString(CultureInfo.CurrentCulture);
        var minuteString = minutes.ToString(CultureInfo.CurrentCulture);
        var secondString = secondsLeft.ToString(CultureInfo.CurrentCulture);
        
        return $"{dayString}d {hourString}h {minuteString}m {secondString}s";
    }
    

    private static void FillWindow(int windowID)
    {
        double ut = GameManager.Instance.Game.UniverseModel.UniversalTime;
        GUILayout.Label("The current in-game time is: " + Ut2String(Math.Round(ut, _utRounding.Value)));
        
        var dayCount = Math.Floor(Math.Round(ut, 0) / 21600);

        var nextSunrise = dayCount * 21600 + _bestSunriseTime.Value;
        
        if (nextSunrise - ut < 0)
        {
            nextSunrise = (dayCount + 1) * 21600 + _bestSunriseTime.Value;
        }
        
        var timeManager = GameManager.Instance.Game.ViewController?.TimeWarp;

        GUILayout.Label("The next sunrise at: " + Ut2String(nextSunrise));
        GUILayout.Label("In: " + Ut2String(Math.Round(nextSunrise - ut, _utRounding.Value)));
        if (GUILayout.Button("Warp to next sunrise"))
        {
            if (timeManager == null)
            {
                return;
            }
            if (!_isEdgeWarping)
            {
                for (int i = 0; i < _sunriseWarpIndex.Value; i++)
                {
                    if (timeManager._currentRate < _sunriseWarpIndex.Value)
                    {
                        timeManager.IncreaseTimeWarp();
                    }
                }
                _dayStartedWarp = (int) dayCount;
                _isEdgeWarping = true;
            }
        }
        
        if (_dayStartedWarp + 2 == (int) dayCount)
        {
            _isEdgeWarping = false;
            for (int i = 0; i < _sunriseWarpIndex.Value; i++)
            {
                if (timeManager?._currentRate < _sunriseWarpIndex.Value)
                {
                    timeManager.DecreaseTimeWarp();
                }
            }

            _dayStartedWarp = -1;
            timeManager?.StopTimeWarp();
        }

        if (_isEdgeWarping)
        {
                    
            if (GUILayout.Button("Stop Warping"))
            {
                timeManager?.StopTimeWarp();
                _isEdgeWarping = false;
            }
            
            GUILayout.Label("Warping to next sunrise...");
            GUILayout.Label(nextSunrise - _timePadding.Value + " > " + Math.Round(ut, _utRounding.Value));
            if (ut > nextSunrise - _timePadding.Value)
            {
                GUILayout.Label("Warping to next sunrise... Done!");
                timeManager?.StopTimeWarp();
                _isEdgeWarping = false;
            }
        }
        if (GUI.Button(new Rect(_windowRect.width - 18, 2, 16, 16), "x"))
        {
            CloseWindow();
            GUIUtility.ExitGUI();
        }
        GUI.DragWindow(new Rect(0, 0, 10000, 500));
    }
}
