using System;
using System.Collections.Generic;
using PopUpWindows;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public partial class UIManager : MonoBehaviour
{
    [Header("Scripts")]
    [SerializeField] private OscSelectionMenu _oscMenu;
    
    [Header("Prefabs")]
    [SerializeField] private GameObject _faderOptionsPrefab;
    [SerializeField] private GameObject _controller2DOptionsPrefab;
    [SerializeField] private GameObject _faderOptionsActivationPrefab; //the prefab for the button that opens up fader options
    
    [Header("Main Fader UI")]
    [SerializeField] private HorizontalLayoutGroup _faderLayoutGroup;
    [SerializeField] private GridLayoutGroup _sliderOptionsButtonLayout;
    [SerializeField] private Button _optionsButton;
    [SerializeField] private Button _closeOptionsButton;
    [SerializeField] private Button _newControllerButton;
    [SerializeField] private Button _faderPositionEnableButton;
    [SerializeField] private Button _faderPositionExitButton;
    [SerializeField] private Button _optionsButtonSortingButton;
    [SerializeField] private Button _setupButton;
    
    [Header("Main Options Panel")]
    [SerializeField] private GameObject _optionsPanel;

    [Header("Setup Menu")]
    [SerializeField] private GameObject _setupPanel;
    [SerializeField] private Button _closeSetupButton;

    private readonly List<ControllerUIGroup> _controllerUIs = new();

    private bool _sortOptionsByName;

    public static UIManager Instance;

    // Start is called before the first frame update
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogError($"There is a second UIManager in the scene!", this);
            Debug.LogError($"This is the first one", Instance);
        }
        //prevent accidentally leaving stuff on in the scene
        _optionsPanel.SetActive(false);

        //options
        _optionsButton.onClick.AddListener(() => ToggleOptionsMenu(true));
        _closeOptionsButton.onClick.AddListener(() => ToggleOptionsMenu(false));
        
        // sorting
        _faderPositionExitButton.onClick.AddListener(() => ToggleSortButtonVisibility(false));
        _faderPositionEnableButton.onClick.AddListener(() => ToggleSortButtonVisibility(true));
        _optionsButtonSortingButton.onClick.AddListener(() => SwitchOptionsButtonSorting(!_sortOptionsByName));
        SwitchOptionsButtonSorting(false);
        

        UnityAction toggleSetup = () => _setupPanel.SetActive(!_setupPanel.activeSelf);
        _setupButton.onClick.AddListener(toggleSetup);
        _closeSetupButton.onClick.AddListener(toggleSetup);

        // initialize grid layout for options buttons
        _sliderOptionsButtonLayout.cellSize = _faderOptionsActivationPrefab.GetComponent<RectTransform>().rect.size;

    }

    void Start()
    {
        _newControllerButton.onClick.AddListener(OpenNewControllerWindow);
    }

    private void SwitchOptionsButtonSorting(bool byName)
    {
        _sortOptionsByName = byName;
        _optionsButtonSortingButton.GetComponentInChildren<Text>().text = $"Options sorted by {(byName ? "name" : "layout")}";
        SortOptionsButtons(byName, _controllerUIs);
        return;

        static void SortOptionsButtons(bool sortOptionsByName, List<ControllerUIGroup> controllerUIs)
        {
            //get all the unnamed ones out of sorting list
            controllerUIs.Sort((s1, s2) =>
            {
                var controller1 = s1.ControllerData;
                var controller2 = s2.ControllerData;
                if (!sortOptionsByName)
                {
                    return controller1.SortPosition.CompareTo(controller2.SortPosition);
                }
                
                var firstName = controller1.Name;
                var secondName = controller2.Name;
                if (firstName == secondName)
                    return 0;

                var firstIsDefault = controller1.IsNamedAsDefault;
                var secondIsDefault = controller2.IsNamedAsDefault;

                if (firstIsDefault ^ secondIsDefault)
                {
                    return firstIsDefault ? -1 : 1;
                }
                
                return string.Compare(firstName, secondName, StringComparison.Ordinal);
            });
            
            for (var i = 0; i < controllerUIs.Count; i++)
            {
                controllerUIs[i].OptionButtonTransform.SetSiblingIndex(i);
            }
        }
    }

    private static void OpenNewControllerWindow()
    {
        var faderAction = new MultiOptionAction("Fader", () => ControlsManager.NewController(ControllerType.Fader));
        var controller2DAction = new MultiOptionAction("2D Controller", () => ControlsManager.NewController(ControllerType.Controller2D));
        //var buttonAction = new MultiOptionAction("Button", () => throw new NotImplementedException());
        //var controllerTemplateAction = new MultiOptionAction("From Template", () => throw new NotImplementedException());
        PopUpController.Instance.MultiOptionWindow("Select a controller type", faderAction,
            controller2DAction); //, buttonAction, controllerTemplateAction);
    }

    //used by options button in scene
    public void ToggleOptionsMenu(bool active)
    {
        var wasActive = _optionsPanel.activeSelf;

        if (wasActive == active)
        {
            Debug.LogError("Tried to toggle options menu when it was already in that state");
            return;
        }
        
        _optionsPanel.SetActive(active);
    }

    #region Controller Options
    public void SpawnControllerOptions(ControllerData config, GameObject control, out Action destroyFunc)
    {
        var group = InitializeControllerOptions(config, control);
        SwitchOptionsButtonSorting(_sortOptionsByName);
        
        destroyFunc = () => DestroyUiGroup(group);
        
        void DestroyUiGroup(ControllerUIGroup group)
        {
            _controllerUIs.Remove(group);
            group.DestroySelf();
            group.DeletionRequested -= OnDeletionRequested;
        }
    }

    private ControllerUIGroup InitializeControllerOptions(ControllerData config, GameObject control)
    {
        var parent = (RectTransform)_sliderOptionsButtonLayout.transform;

        Func<GameObject> createOptionsMenuFunc = config switch
        {
            FaderData fader => () => InitializeFaderOptions(fader),
            Controller2DData control2D => () => InitializeController2DOptions(control2D),
            _ => throw new ArgumentOutOfRangeException(nameof(config), config, null)
        };

        var buttonGroup = new ControllerUIGroup(config, _faderOptionsActivationPrefab, parent,
            control.GetComponent<ISortingMember>(), createOptionsMenuFunc);

        buttonGroup.DeletionRequested += OnDeletionRequested;

        _controllerUIs.Add(buttonGroup);
        return buttonGroup;
    }

    private void OnDeletionRequested(object sender, EventArgs e)
    {
        var group = (ControllerUIGroup)sender;
        var data = group.ControllerData;

        PopUpController.Instance.ConfirmationWindow(
            text: $"Delete controller\n\"{group.ControllerData.Name}\"?", 
            confirm: () =>
            {
                data.DeletionRequested = true;
                data.InvokeDestroyed();
            },
            cancel: null, 
            confirmButtonLabel: "Delete", 
            cancelButtonLabel: "Cancel");
    }

    private GameObject InitializeFaderOptions(FaderData config)
    {
        var menuObj = Instantiate(_faderOptionsPrefab, _optionsPanel.transform, false);
        var faderOptions = menuObj.GetComponent<FaderOptionsPanel>();
        faderOptions.Initialize(config, _oscMenu);
        faderOptions.gameObject.name = config.Name + " Options Panel";
        return menuObj;
    }

    private GameObject InitializeController2DOptions(Controller2DData config)
    {
        var menuObj = Instantiate(_controller2DOptionsPrefab, _optionsPanel.transform, false);
        var controllerOptions = menuObj.GetComponent<Controller2DOptionsPanel>();
        controllerOptions.Initialize(config, _oscMenu);
        controllerOptions.gameObject.name = config.Name + " Options Panel";
        return menuObj;
    }

    public void ShowControllerOptions(ControllerData data)
    {
        var uiGroup = GetButtonGroupByConfig(data, false);
        uiGroup.SetControllerOptionsActive(true);
    }

    private ControllerUIGroup GetButtonGroupByConfig(ControllerData data, bool removeIfFound)
    {
        for (var index = 0; index < _controllerUIs.Count; index++)
        {
            var cbg = _controllerUIs[index];
            if (cbg.ControllerData == data)
            {
                if (removeIfFound)
                    _controllerUIs.RemoveAt(index);
                
                return cbg;
            }
        }

        return null;
    }
    #endregion Controller Options

    /// <summary>
    /// This is used to let the controllers use their own width - for some reason this is necessary for the width to update
    /// </summary>
    public void RefreshFaderLayoutGroup()
    {
        _faderLayoutGroup.enabled = false;
        _faderLayoutGroup.enabled = true;
    }

    private void ToggleSortButtonVisibility(bool visible)
    {
        foreach(var u in _controllerUIs)
        {
            u.SortingImpl.SetSortButtonVisibility(visible);
            u.ControllerData.SetPosition(u.SortingImpl.RectTransform.GetSiblingIndex());
        }

        //toggle options buttons
        _optionsButton.gameObject.SetActive(!visible);
        _optionsPanel.SetActive(!visible);
        _faderPositionExitButton.gameObject.SetActive(visible);
    }
}
