using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public class ControlsManager : MonoBehaviour
{
    #region MonoBehaviour
    [SerializeField] private RectTransform _controllerParent;
    [SerializeField] private ControllerPrefabs[] _controllerPrefabs;

    private void Awake()
    {
        _controllerParentInstance = _controllerParent;
        _controllerPrefabsInstance = _controllerPrefabs.ToDictionary(x => x.ControlType, x => x.ControlPrefab);
        ProfilesManager.ProfileChanged += ApplyProfile;
    }

    private void Update()
    {
        while(ActionQueue.TryDequeue(out var action))
            action.Invoke();
    }
    
    #endregion MonoBehaviour

    private static readonly Dictionary<ControllerData, GameObject> ControllerObjects = new();
    private static readonly Queue<Action> ActionQueue = new();
    private static ProfilesManager _profilesManagerInstance;
    private static RectTransform _controllerParentInstance;
    private static Dictionary<ControllerType, GameObject> _controllerPrefabsInstance;
    
    internal static ProfileSaveData ActiveProfile;
    

    #region Default Controller Values

    private static readonly Dictionary<BuiltInOscPreset, OscControllerSettings> DefaultOscSettings = new()
    {
        {BuiltInOscPreset.Pitch,            OscControllerSettings.DefaultOscTemplates[OscAddressType.MidiPitch] },
        {BuiltInOscPreset.Mod,              OscControllerSettings.DefaultOscTemplates[OscAddressType.MidiCc] },
        {BuiltInOscPreset.Aftertouch,       OscControllerSettings.DefaultOscTemplates[OscAddressType.MidiAftertouch] },
        {BuiltInOscPreset.FootPedal,        new OscControllerSettings(OscAddressType.MidiCc,            MidiChannel.All, ValueRange.SevenBit,       4) },
        {BuiltInOscPreset.Expression,       new OscControllerSettings(OscAddressType.MidiCc,            MidiChannel.All, ValueRange.SevenBit,       11) },
        {BuiltInOscPreset.BreathControl,    new OscControllerSettings(OscAddressType.MidiCc,            MidiChannel.All, ValueRange.SevenBit,       2) },
        {BuiltInOscPreset.Volume,           new OscControllerSettings(OscAddressType.MidiCc,            MidiChannel.All, ValueRange.SevenBit,       7) },
    };

    internal static readonly List<ControllerData> DefaultControllers = new()
    {
        new FaderData(new AxisControlSettings(InputMethod.Touch,    ReleaseBehaviorType.PitchWheel,    DefaultOscSettings[BuiltInOscPreset.Pitch],         DefaultValueType.Mid, CurveType.Linear), "Pitch"),
        new FaderData(new AxisControlSettings(InputMethod.Touch,    ReleaseBehaviorType.Normal,        DefaultOscSettings[BuiltInOscPreset.Mod],           DefaultValueType.Mid, CurveType.Linear), "Mod"),
        new FaderData(new AxisControlSettings(InputMethod.Touch,    ReleaseBehaviorType.Normal,        DefaultOscSettings[BuiltInOscPreset.FootPedal],     DefaultValueType.Mid, CurveType.Linear), "Foot Pedal"),
        new FaderData(new AxisControlSettings(InputMethod.Touch,    ReleaseBehaviorType.Normal,        DefaultOscSettings[BuiltInOscPreset.Expression],    DefaultValueType.Mid, CurveType.Linear), "Expression"),
        new FaderData(new AxisControlSettings(InputMethod.Touch,    ReleaseBehaviorType.Normal,        DefaultOscSettings[BuiltInOscPreset.BreathControl], DefaultValueType.Mid, CurveType.Linear), "Breath Control"),
        new FaderData(new AxisControlSettings(InputMethod.Touch,    ReleaseBehaviorType.Normal,        DefaultOscSettings[BuiltInOscPreset.Aftertouch],    DefaultValueType.Mid, CurveType.Linear), "Aftertouch"),
        new FaderData(new AxisControlSettings(InputMethod.Touch,    ReleaseBehaviorType.Normal,        DefaultOscSettings[BuiltInOscPreset.Volume],        DefaultValueType.Mid, CurveType.Linear), "Volume")
    };

    private enum BuiltInOscPreset
    {
        Pitch, Mod, FootPedal, Expression, BreathControl, Aftertouch, Volume
    };

    private static readonly FaderData DefaultFader = new(new AxisControlSettings(InputMethod.Touch, ReleaseBehaviorType.Normal, DefaultOscSettings[BuiltInOscPreset.Mod],         DefaultValueType.Min, CurveType.Linear), null);

    private static readonly Controller2DData DefaultController2D = new(new AxisControlSettings(InputMethod.Touch, ReleaseBehaviorType.Normal, DefaultOscSettings[BuiltInOscPreset.Mod],         DefaultValueType.Min, CurveType.Linear),
        new AxisControlSettings(InputMethod.Touch, ReleaseBehaviorType.Normal, DefaultOscSettings[BuiltInOscPreset.Expression],  DefaultValueType.Min, CurveType.Linear), null);
    #endregion Default Controller Values

    private static void ApplyProfile(ProfileSaveData profile)
    {
        if (ActiveProfile != null)
        {
            Debug.Log($"Unloading {ActiveProfile.Name}");
            while (ControllerObjects.Count > 0)
            {
                var (controllerData, _) = ControllerObjects.Last();
                controllerData.InvokeDestroyed();
            }
        }
        
        ActiveProfile = profile;
        
        foreach(var c in profile.AllControllers)
        {
            _ = InstantiateControllerUi(c);
        }
        
        FixControllerSorting();
    }

    internal static void NewController(ControllerType type)
    {
        ControllerData controlData;

        switch (type)
        {
            case ControllerType.Fader:
                controlData = new FaderData(DefaultFader.Settings);
                break;
            case ControllerType.Controller2D:
                controlData = new Controller2DData(DefaultController2D.HorizontalAxisControl, DefaultController2D.VerticalAxisControl);
                break;
            default:
                Debug.LogError($"Controller type {type} not implemented in {nameof(ControlsManager)}");
                return;
        }

        ActiveProfile.AddController(controlData);
        var newController = InstantiateControllerUi(controlData);
        controlData.SetPosition(newController.transform.GetSiblingIndex());
        UIManager.Instance.ShowControllerOptions(controlData);
    }

    private static void FixControllerSorting()
    {
        // order transforms by order of their sorting index,
        // then overwrite their indices with their new sibling index

        var sorted = ControllerObjects.OrderBy(x => x.Key.SortPosition)
            .ToArray();

        int index = 0;
        foreach (var (data, obj) in sorted)
        {
            data.SetPosition(index);
            obj.transform.SetSiblingIndex(index);
            ++index;
        }
    }

    private static GameObject InstantiateControllerUi(ControllerData data)
    {
        //spawn this type
        var control = CreateAndInitializeControllerUi(data);
        ControllerObjects.Add(data, control);
        data.DeletionRequested = false; // unnecessary, but since it is a public property, can't be too careful

        if(!data.Enabled)
        {
            //do this after a frame to allow the controller to run its Start method
            ActionQueue.Enqueue(() => control.SetActive(false));
        }

        UIManager.Instance.SpawnControllerOptions(data, control, out var destroyFunc);
        data.OnDestroyRequested = (sender, _) =>
        {
            destroyFunc.Invoke();
            var controlData = (ControllerData)sender;
            controlData.OnDestroyRequested = null;

            if (controlData.DeletionRequested && !ActiveProfile.RemoveController(controlData))
            {
                throw new Exception($"Failed to remove controller {controlData.Name} from profile {ActiveProfile.Name}");
            }
            
            controlData.DeletionRequested = false;

            if (ControllerObjects.Remove(controlData, out var obj))
            {
                Destroy(obj);
            }
        };

        return control;

        static GameObject CreateAndInitializeControllerUi(ControllerData config)
        {
            var controlPrefab = _controllerPrefabsInstance[config.ControlType];
            var controlObj = Instantiate(controlPrefab, _controllerParentInstance, false);

            switch (config)
            {
                case FaderData fader:
                    controlObj.GetComponentInChildren<FaderControlUi>().Initialize(fader);
                    break;
                case Controller2DData control2D:
                    controlObj.GetComponentInChildren<Controller2DUi>().Initialize(control2D);
                    break;
                default:
                    Debug.Log($"No way to spawn {config.GetType()}");
                    break;
            }

            return controlObj;
        }
    }

    //used to pair prefabs with their control type
    [Serializable]
    private struct ControllerPrefabs
    {
        [FormerlySerializedAs("controlType")] public ControllerType ControlType;
        [FormerlySerializedAs("controlPrefab")] public GameObject ControlPrefab;
    }
}
