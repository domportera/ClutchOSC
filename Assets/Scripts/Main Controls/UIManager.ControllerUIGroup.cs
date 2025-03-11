using System;
using UnityEngine;
using UnityEngine.UI;

public partial class UIManager
{
    private class ControllerUIGroup
    {
        public RectTransform OptionButtonTransform { get; }
        public readonly ISortingMember SortingImpl;
        public ControllerData ControllerData { get; }
        private GameObject _optionsMenu;
        private readonly Func<GameObject> _createOptionsMenu;

        public event EventHandler DeletionRequested;
        private Text _buttonText;

        public ControllerUIGroup(ControllerData config, GameObject optionsActivateButtonPrefab, RectTransform optionsButtonParent, ISortingMember controlObject, Func<GameObject> createOptionsMenu)
        {
            ControllerData = config;
            _createOptionsMenu = createOptionsMenu;
            SortingImpl = controlObject;
            SortingImpl.SetSortButtonVisibility(false);

            var buttonObj = Instantiate(optionsActivateButtonPrefab, optionsButtonParent, false);
            OptionButtonTransform = (RectTransform)buttonObj.transform;
            var activateOptionsButton = buttonObj.GetComponentInChildren<ButtonExtended>();
            activateOptionsButton.gameObject.name = config.Name + " Options Button";
            
            _buttonText = activateOptionsButton.GetComponentInChildren<Text>();
            OnNameChanged(this, config.Name);
            activateOptionsButton.OnClick.AddListener(() => { SetControllerOptionsActive(true); });
            activateOptionsButton.OnPointerHeld.AddListener(Delete);

            config.NameChanged += OnNameChanged;
            
            SetControllerOptionsActive(false);

            var activationToggle = buttonObj.GetComponentInChildren<Toggle>();
            activationToggle.onValueChanged.AddListener(ToggleControlVisibility);
            activationToggle.SetIsOnWithoutNotify(config.Enabled);
        }

        private void OnNameChanged(object sender, string e)
        {
            // change the button text to the title of the controller
            _buttonText.text = e;
        }

        private void Delete()
        {
            DeletionRequested?.Invoke(this, EventArgs.Empty);
        }

        public void SetControllerOptionsActive(bool active)
        {
            if (active)
            {
                if (!_optionsMenu)
                    _optionsMenu = _createOptionsMenu();
                
                _optionsMenu.SetActive(true);
                return;
            }
            
            if(_optionsMenu)
                Destroy(_optionsMenu);
        }

        private void ToggleControlVisibility(bool b)
        {
            ControllerData.SetEnabled(b);

            if(!b)
            {
                SortingImpl.RectTransform.SetAsLastSibling();
            }

            ControlsManager.ForceToggleWidthRefresh();
        }

        public void DestroySelf()
        {
            if(_optionsMenu)
                Destroy(_optionsMenu);
            Destroy(OptionButtonTransform.gameObject);
            _buttonText = null;
            ControllerData.NameChanged -= OnNameChanged;
        }
    }
}