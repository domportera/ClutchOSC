using UnityEngine;

public class FaderOptionsPanel : ControllerOptionsPanel
{
   [SerializeField]
    private ControllerOptionsMenu _optionsMenu;

    public void Initialize(FaderData data, OscSelectionMenu oscMenu)
    {
        BaseInitialize(data);
        _optionsMenu.Initialize(data.Settings, this, oscMenu);

        OnWake += () => _optionsMenu.ResetValues();
    }

    protected override void Apply()
    {
        _optionsMenu.SetControllerValuesToFields();
        base.Apply();
    }

}
