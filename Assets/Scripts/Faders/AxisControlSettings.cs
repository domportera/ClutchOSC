using UnityEngine;

[System.Serializable]
public class AxisControlSettings
{
    [SerializeField] private ReleaseBehaviorType _releaseBehavior;
    [SerializeField] private InputMethod _inputType;
    [SerializeField] private DefaultValueType _defaultType;
    [SerializeField] private float _smoothTime;
    [SerializeField] private CurveType _curveType;
    [SerializeField] private OscControllerSettings _oscSettings;

    public ReleaseBehaviorType ReleaseBehavior => _releaseBehavior;
    public InputMethod InputType => _inputType;
    public DefaultValueType DefaultType => _defaultType;
    public float SmoothTime => _smoothTime;
    public CurveType Curve => _curveType;
    public OscControllerSettings OscSettings => _oscSettings;

    public AxisControlSettings(AxisControlSettings settings)
    {
        SetVariables(settings._inputType, settings._releaseBehavior, settings._oscSettings, settings._defaultType, settings._curveType, settings._smoothTime);
    }

    public AxisControlSettings(InputMethod inputType, ReleaseBehaviorType controlType, OscControllerSettings oscSettings, DefaultValueType defaultValueType, CurveType curveType,  float smoothTime = 0.1f)
    {
        SetVariables(inputType, controlType, oscSettings, defaultValueType, curveType, smoothTime);
    }

    public void SetVariables(InputMethod inputType, ReleaseBehaviorType controlType, OscControllerSettings oscSettings,  DefaultValueType defaultValueType, CurveType curveType, float smoothTime)
    {
        _oscSettings = oscSettings;
        _releaseBehavior = controlType;
        _defaultType = defaultValueType;
        _smoothTime = smoothTime;
        _curveType = curveType;
        _inputType = inputType;
    }

    public string GetAddress()
    {
        return OscSettings.GetAddress();
    }

    public override string ToString()
    {
        var result = $"Control Type: {_releaseBehavior}\n" +
                     $"Input Type: {_inputType}\n" +
                     $"Default Value: {_defaultType}\n";

        return result;
    }
}
