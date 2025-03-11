using UnityEngine;
using UnityEngine.Serialization;

public class Controller2DOptionsPanel : ControllerOptionsPanel
{
    [FormerlySerializedAs("horizontalOptions")] [SerializeField]
    private ControllerOptionsMenu _horizontalOptions;
    [FormerlySerializedAs("verticalOptions")] [SerializeField]
    private ControllerOptionsMenu _verticalOptions;

    public void Initialize(Controller2DData data, OscSelectionMenu oscMenu)
    {
        BaseInitialize(data);
        _horizontalOptions.Initialize(data.HorizontalAxisControl, this, oscMenu);
        _verticalOptions.Initialize(data.VerticalAxisControl, this, oscMenu);

        OnWake += () =>
        {
            _horizontalOptions.ResetValues();
            _verticalOptions.ResetValues();
        };
    }

    protected override void Apply()
    {
        _horizontalOptions.SetControllerValuesToFields();
        _verticalOptions.SetControllerValuesToFields();
        base.Apply();
    }
}
