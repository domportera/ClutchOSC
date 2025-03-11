using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PopUpWindows;

public abstract class ControllerOptionsPanel : MonoBehaviour
{
    [SerializeField] protected InputField NameField;
    [SerializeField] protected Button ApplyButton;
    [SerializeField] protected Button CloseButton;
    [SerializeField] protected Slider WidthSlider;

    private ControllerData _controlData;

    private const int WidthSliderSteps = 7;
    private const int WidthSliderStepsMax = WidthSliderSteps - 1;

    public event Action OnWake;
    public event Action OnSleep;
    
    private bool IsInitialized => _controlData != null;

    protected void Awake()
    {
        NameField.onValueChanged.AddListener(RemoveProblemCharactersInNameField);
        NameField.characterValidation = InputField.CharacterValidation.None;
        ApplyButton.onClick.AddListener(Apply);
        CloseButton.onClick.AddListener(Close);
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }

    protected void OnEnable()
    {
        OnWake?.Invoke();
        if (IsInitialized)
        {
            ResetUiFields();
        }
    }

    protected void OnDisable()
    {
        OnSleep?.Invoke();
    }

    public void ResetUiFields()
    {
        WidthSlider.SetValueWithoutNotify(_controlData.Width);
        NameField.SetTextWithoutNotify(_controlData.Name);
    }

    protected void BaseInitialize(ControllerData data)
    {
        if(IsInitialized)
            throw new Exception($"{GetType().Name} can only be initialized once");
        
        _controlData = data;
        InitializeWidthSlider();
        NameField.characterValidation = InputField.CharacterValidation.None;
        ResetUiFields();
    }

    private void RemoveProblemCharactersInNameField(string input)
    {
        input = input.Replace("\"", "").Replace("\\", "");
        NameField.SetTextWithoutNotify(input);
    }

    protected virtual void Apply()
    {
        if (!VerifyUniqueName(NameField.text))
            return;
        var controllerName = NameField.text;
        _controlData.SetName(controllerName);

        var width = ConvertSliderValueToWidth((int)WidthSlider.value);
        _controlData.SetWidth(width, ControlsManager.ActiveProfile);
        UIManager.Instance.RefreshFaderLayoutGroup();
        Close();
    }

    #region Width

    private void InitializeWidthSlider()
    { 
        var widthRange = _controlData.GetWidthRange();
        WidthSlider.wholeNumbers = true;
        WidthSlider.minValue = ConvertWidthToSliderValue(widthRange.Min);
        WidthSlider.maxValue = ConvertWidthToSliderValue(widthRange.Max);
    }

    //all this conversion stuff is to achieve the stepping slider with the multiplicative fractional width functionality
    private int ConvertWidthToSliderValue(float width)
    {
        var widthRange = _controlData.GetWidthRange();
        return (int)width.Map(widthRange.Min, widthRange.Max, 0, WidthSliderStepsMax);
    }

    private float ConvertSliderValueToWidth(int value)
    {
        var widthRange = _controlData.GetWidthRange();
        return ((float)value).Map(0, WidthSliderStepsMax, widthRange.Min, widthRange.Max);
    }
    #endregion Width

    private bool VerifyUniqueName(string potentialName)
    {
        if (string.IsNullOrWhiteSpace(potentialName))
        {
            PopUpController.Instance.ErrorWindow("Name cannot be empty.");
            return false;
        }
        
        var invalid = ControlsManager.ActiveProfile.AllControllers
            .Where(data => data != _controlData)
            .Any(data => data.Name == potentialName);

        if (!invalid) return true;
        
        PopUpController.Instance.ErrorWindow("Name should be unique - no two controllers in the same profile can have the same name.");
        return false;
    }
}
