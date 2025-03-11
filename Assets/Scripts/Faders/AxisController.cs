using System;
using System.Collections;
using UnityEngine;

public sealed class AxisController
{
    private readonly AxisControlSettings _axisControlSettings;
    private OscControllerSettings OscSettings => _axisControlSettings.OscSettings;

    private float _targetControllerValue;
    private bool _arrivedAtTarget = false;

    public string LatestSentValue
    {
        get
        {
            if (_previousValueStr != null && !_hasSentNewValue) 
                return _previousValueStr;
            
            _previousValueStr = _axisControlSettings.OscSettings.Range switch
            {
                ValueRange.SevenBit or ValueRange.EightBit or ValueRange.CustomInt => _latestSentValue.ToString(
                    "000"),
                ValueRange.FourteenBit => _latestSentValue.ToString("00000"),
                ValueRange.Float or ValueRange.CustomFloat => _latestSentValue.ToString("0.000"),
                _ => throw new ArgumentOutOfRangeException()
            };

            return _previousValueStr;
        }
    }
    
    private float _latestSentValue;
    private string _previousValueStr;
    private bool _hasSentNewValue = false;

    private float DefaultValue
    {
        get
        {
            switch (_axisControlSettings.DefaultType)
            {
                case DefaultValueType.Min:
                    return MinControllerValue;
                case DefaultValueType.Mid:
                    return (MinControllerValue + MaxControllerValue) / 2f;
                case DefaultValueType.Max:
                    return MaxControllerValue;
                default:
                    Debug.LogError("Default value type not implemented! Defaulting to min.");
                    return MinControllerValue;
            }
        }
    }


    /// <summary>
    /// returns moving mod value as it approaches target value
    /// </summary>
    public float SmoothValue { get; private set; }

    public const float MinControllerValue = 0f;
    public const float MaxControllerValue = 1f;

    public AxisController(AxisControlSettings settings)
    {
        _axisControlSettings = settings ?? throw new ArgumentNullException(nameof(settings));

        var defaultValue = DefaultValue;
        SmoothValue = defaultValue;
        _targetControllerValue = defaultValue;
        SendCurrentValue();
    }

    #region Mod Value Manipulation
    public void SetValue(float val)
    {
        _arrivedAtTarget = _arrivedAtTarget && Mathf.Approximately(val, _targetControllerValue);
        _targetControllerValue = val;
    }

    private float MapValueToCurve(float value, bool inverse)
    {
        var exponent = _axisControlSettings.Curve.GetExponent();
        if(inverse)
            exponent = 1f/exponent;
        
        const float range = MaxControllerValue - MinControllerValue;
        var ratio = (value - MinControllerValue) / range;
        var mappedRatio = Math.Pow(ratio, exponent);
        return (float)(mappedRatio * range + MinControllerValue);
    }

    public void Update(float deltaTime)
    {
        if (_arrivedAtTarget)
        {
            return;
        }

        var shouldSmooth = _axisControlSettings.SmoothTime > 0;
        if (!shouldSmooth)
        {
            SmoothValue = _targetControllerValue;
            _arrivedAtTarget = true;
        }
        else
        {
            UpdateSmoothly();
        }

        SendCurrentValue();
        return;

        void UpdateSmoothly()
        {
            var difference = (MaxControllerValue - MinControllerValue) * deltaTime / _axisControlSettings.SmoothTime;

            //set to idle if close enough to zero
            if (Mathf.Abs(SmoothValue - _targetControllerValue) < difference)
            {
                SmoothValue = _targetControllerValue;
                _arrivedAtTarget = true;
            }
            else
            {
                //approach target value
                if (SmoothValue > _targetControllerValue)
                {
                    SmoothValue -= difference;
                }
                else
                {
                    SmoothValue += difference;
                }
            }
        }
    }

    public void Release()
    {
        switch (_axisControlSettings.ReleaseBehavior)
        {
            case ReleaseBehaviorType.PitchWheel:
                SetValue(DefaultValue);
                break;
            default:
                return;
        }
    }
    
    #endregion Mod Value Manipulation

    #region OSC Communication

    private void SendCurrentValue()
    {
        var curveMappedValue = MapValueToCurve(SmoothValue, false);

        var isFloat = OscSettings.Range is ValueRange.CustomFloat or ValueRange.Float;
        var oscSettings = _axisControlSettings.OscSettings;
        var address = oscSettings.GetAddress();
        float latestSentValue;
        if (isFloat)
        {
            var val = oscSettings.GetValueFloat(curveMappedValue);
            OSCSystem.Send(address, val);
            latestSentValue = val;
        }
        else
        {
            var val = oscSettings.GetValueInt(curveMappedValue);
            OSCSystem.Send(address, val);
            latestSentValue = val;
        }
        
        _hasSentNewValue = !Mathf.Approximately(_latestSentValue, latestSentValue);
        _latestSentValue = latestSentValue;
    }

    private IEnumerator SendModValueMultipleTimes(int numberOfTimes)
    {
        for(var i = 0; i < numberOfTimes; i++)
        {
            SendCurrentValue();
            yield return null;
        }
    }

    #endregion OSC Communication

}

internal interface ISortingMember
{
    void SetSortButtonVisibility(bool visible);
    RectTransform RectTransform { get; }
}
