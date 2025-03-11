using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ControllerOptionsMenu : MonoBehaviour
{
    private AxisControlSettings _axisControlConfig;

    [SerializeField] private Slider _smoothnessField;
    [SerializeField] private Dropdown _releaseBehaviourButton;
    [SerializeField] private Dropdown _defaultValueDropdown;
    [SerializeField] private Dropdown _curveTypeDropdown;
    [SerializeField] private Button _resetValuesButton;
    [SerializeField] private Button _openOscOptionsButton;

    private readonly Dictionary<Dropdown, string[]> _dropDownEntryNames = new ();
    private OscControllerSettings _oscSettingsPendingApplication;

    private bool _initialized;

    private Action _onDestroy;
    private Action _resetValuesAction;

    private void Awake()
    {
        PopulateDropdowns();
        _resetValuesButton.onClick.AddListener(ResetValues);
    }

    private void OnEnable()
    {
        if (!_initialized) return;
        ResetValues();
    }

    public void Initialize(AxisControlSettings data, ControllerOptionsPanel optionsPanel, OscSelectionMenu oscMenu)
    {
        if(_axisControlConfig != null)
            throw new Exception($"{GetType().Name} can only be initialized once");
        
        _axisControlConfig = data ?? throw new ArgumentNullException(nameof(data));

        oscMenu.Changed += OnOscMenuOnChanged;
        optionsPanel.OnWake += ClearPendingChanges;

        _onDestroy = () =>
        {
            oscMenu.Changed -= OnOscMenuOnChanged;
            optionsPanel.OnWake -= ClearPendingChanges;
        };

        _resetValuesAction = optionsPanel.ResetUiFields;

        _openOscOptionsButton.onClick.AddListener(() => oscMenu.OpenWith(_axisControlConfig.OscSettings));
        
        ResetValues();
        _initialized = true;
    }

    private void OnDestroy()
    {
        _onDestroy();
    }

    private void ClearPendingChanges()
    {
        _oscSettingsPendingApplication = null;
    }

    void OnOscMenuOnChanged(OscControllerSettings settings)
    {
        if (settings != _axisControlConfig.OscSettings) return;

        _oscSettingsPendingApplication = new OscControllerSettings(settings);
        UpdateOscPreview(settings);
    }

    public void SetControllerValuesToFields()
    {
        var controlType = (ReleaseBehaviorType)_releaseBehaviourButton.value;
        var defaultValueType = (DefaultValueType)_defaultValueDropdown.value;
        var curveType = (CurveType)_curveTypeDropdown.value;
        var inputType = InputMethod.Touch; //hard-coded for now until other input types are implemented

        var smoothTime = _smoothnessField.value;

        var oscSettings = _oscSettingsPendingApplication != null 
            ? new OscControllerSettings(_oscSettingsPendingApplication) 
            : _axisControlConfig.OscSettings;

        _axisControlConfig.SetVariables(inputType, controlType, oscSettings, defaultValueType, curveType, smoothTime);
    }

    private void UpdateOscPreview(OscControllerSettings settings)
    {
        _openOscOptionsButton.GetComponentInChildren<Text>().text = $"<b>OSC Options</b>\n{settings.GetAddress()}";
    }

    private void PopulateDropdowns()
    {
        _dropDownEntryNames.Add(_releaseBehaviourButton, EnumUtility.GetTypeNameArray<ReleaseBehaviorType>());
        _dropDownEntryNames.Add(_defaultValueDropdown, EnumUtility.GetTypeNameArray<DefaultValueType>());
        _dropDownEntryNames.Add(_curveTypeDropdown, EnumUtility.GetTypeNameArray<CurveType>());

        foreach (var pair in _dropDownEntryNames)
        {
            pair.Key.ClearOptions();
            foreach (var s in pair.Value)
            {
                pair.Key.options.Add(new Dropdown.OptionData(s));
            }
        }
    }

    public void ResetValues()
    {
        _releaseBehaviourButton.SetValueWithoutNotify((int)_axisControlConfig.ReleaseBehavior);
        _defaultValueDropdown.SetValueWithoutNotify((int)_axisControlConfig.DefaultType);
        _curveTypeDropdown.SetValueWithoutNotify((int)_axisControlConfig.Curve);
        _smoothnessField.SetValueWithoutNotify(_axisControlConfig.SmoothTime);
        _resetValuesAction();
        
        UpdateOscPreview(_axisControlConfig.OscSettings);
    }
}