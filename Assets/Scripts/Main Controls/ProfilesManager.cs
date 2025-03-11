using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

using UnityEngine.Serialization;
using PopUpWindows;

public class ProfilesManager : MonoBehaviour
{
    [Header("Profiles Window")]
    [SerializeField]
    private Button _toggleProfileWindowButton, _closeProfileWindowButton;
    [SerializeField] private GameObject _profileWindow;

    [Header("Title Text")]
    [SerializeField]
    private Text _titleText;
    [SerializeField] private bool _boldTitleText = true;

    [Header("Profiles")]
    [SerializeField]
    private Button _saveButton, _setDefaultButton;

    [Header("Save Profiles As")]
    [SerializeField]
    private Button _saveAsButton;
    
    [Header("Profile Options")]
    [SerializeField] 
    private Toggle _toggleSpanWidth;
    
    [Header("Dynamic UI Elements")]
    [SerializeField]
    private GameObject _profileLoadButtonPrefab;
    [SerializeField] private Transform _profileButtonParent;

    private static readonly Dictionary <ProfileSaveData, ProfileButtonUi> ProfileButtons = new();
    //saving variables
    public const string DefaultSaveName = "Default";
    private const string ProfileNameSaveName = "Profiles"; //name of json file that stores all profile names
    private static ProfilesMetadata _profileMetadata;

    private static string _basePath;
    private const string ControlsExtension = ".json";
    private const string MetadataExtension = ".json";

    private static ProfilesManager _singleInstance;
    
    public static event Action<ProfileSaveData> ProfileChanged;
    public static event Action<ProfileSaveData> ProfileSaved;

    private void Awake()
    {
        if(_singleInstance != null)
            throw new Exception("Only one instance of ProfilesManager can exist at a time");

        _singleInstance = this;
        
        _basePath = Path.Combine(Application.persistentDataPath, "Controllers");

        InitializeUIElements();
        _profileMetadata = LoadProfileMetadata();
        var directoryInfo = new DirectoryInfo(_basePath);
        if (directoryInfo.Exists)
        {
            var allSavedProfiles =
                Directory.EnumerateFiles(_basePath, $"*{ControlsExtension}")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Select(fileName => TryLoadProfile(fileName, out var data) ? data : null)
                    .Where(x => x != null);

            foreach (var profile in allSavedProfiles)
            {
                AddToProfileButtons(profile);
            }
        }
        else
        {
            directoryInfo.Create();
        }
        
        var defaultProfile = new ProfileSaveData(ControlsManager.DefaultControllers, DefaultSaveName, false);
        AddToProfileButtons(defaultProfile);

        SortProfileButtons();
        
        ProfileChanged += profile => Debug.Log($"Profile changed to {profile.Name}", this);
        ProfileChanged += profile => _toggleSpanWidth.SetIsOnWithoutNotify(profile.SpanWidth);
        _toggleSpanWidth.onValueChanged.AddListener(OnSpanWidthToggled);
        SetActiveProfile(GetOrUpdateDefaultProfile());
    }

    private void OnDestroy()
    {
        if(_singleInstance != this)
            throw new Exception("There should only be one instance of ProfilesManager");
        
        _singleInstance = null;
    }

    private void InitializeUIElements()
    {
        //profiles
        _saveButton.onClick.AddListener(Save);

        //save profiles as
        _saveAsButton.onClick.AddListener(SaveAsWindow);

        //set default profile
        _setDefaultButton.onClick.AddListener(SetDefaultProfile);

        //enable disable profile window
        _toggleProfileWindowButton.onClick.AddListener(ToggleProfileWindow);
        _closeProfileWindowButton.onClick.AddListener(ToggleProfileWindow);

        _titleText.supportRichText = _boldTitleText;
    }

    private void DeleteConfirmation(ProfileSaveData profile)
    {
        if (profile.IsBuiltInDefault)
        {
            PopUpController.Instance.ErrorWindow($"Can't delete default profile");
            return;
        }

        var activeProfile = ControlsManager.ActiveProfile;
        
        if(activeProfile == null)
            throw new Exception("No active profile");

        var confirmationWindowText = activeProfile == profile ?
        "Are you sure you want to delete your current profile? This will automatically load the default profile after deletion."
            : $"Are you sure you want to delete profile {profile.Name}?";

        PopUpController.Instance.ConfirmationWindow(text: confirmationWindowText, 
            confirm: () => DeleteProfileAndUpdateUi(profile),
            cancel: null, 
            confirmButtonLabel: "Delete", 
            cancelButtonLabel: "Cancel");
    }

    private void SaveAsWindow()
    {
        PopUpController.Instance.TextInputWindow($"Enter a name for your new profile:", SaveAs, null, "Save", "Cancel");
    }

    private ProfileSaveData GetOrUpdateDefaultProfile()
    {
        var defaultProfileName = _profileMetadata.DefaultProfileName;
        ProfileSaveData defaultProfile = null;

        foreach(var profile in ProfileButtons.Keys)
        {
            if(profile.Name == defaultProfileName)
            {
                defaultProfile = profile;
                break;
            }
        }
        
        if (defaultProfile == null)
        {
            defaultProfile = new ProfileSaveData(ControlsManager.DefaultControllers, DefaultSaveName, false);
            AddToProfileButtons(defaultProfile);
            SortProfileButtons();
        }
        
        return defaultProfile;
        
    }

    private void AddToProfileButtons(ProfileSaveData profile)
    {
        var profileName = profile.Name;
        var obj = Instantiate(_profileLoadButtonPrefab, _profileButtonParent);
        var buttonScript = obj.GetComponent<ProfileButtonUi>();
        buttonScript.SetText(profileName);
        buttonScript.SetButtonActions(
            pressAction: () => SetActiveProfile(profile), 
            holdAction: () => DeleteConfirmation(profile));
        
        buttonScript.ToggleHighlight(false);
        ProfileButtons.Add(profile, buttonScript);

        Debug.Log($"Adding profile button {profileName}", this);
    }

    private void SortProfileButtons()
    {
        foreach(var (profile, ui) in ProfileButtons.OrderBy(x => x.Key.Name))
        {
            if (profile.IsBuiltInDefault) continue; // keeps default on top

            // hacky way to move to end of list in hierarchy
            var uiTransform = ui.transform;
            uiTransform.SetParent(null);
            uiTransform.SetParent(_profileButtonParent);
        }
    }

    private void SetActiveProfile(ProfileSaveData profileData)
    {
        ProfileChanged?.Invoke(profileData);
        foreach (var p in ProfileButtons.Values)
        {
            p.ToggleHighlight(false);
        }

        var buttonUi = ProfileButtons[profileData];
        buttonUi.ToggleHighlight(true);
        _titleText.text = $"<b>{profileData.Name}</b>";
    }

    private void Save()
    {
        var profile = ControlsManager.ActiveProfile;
        if (profile == null)
            return;
        
        _ = SaveProfile(profile);
    }

    private void SaveAs(string saveName)
    {
        if (!SaveProfileAs(saveName, out var newProfile)) return;
        
        AddToProfileButtons(newProfile);
        SortProfileButtons();
        ProfileSaved?.Invoke(newProfile);
        SetActiveProfile(newProfile);
    }

    private static void SetDefaultProfile()
    {
        var activeProfile = ControlsManager.ActiveProfile;
        _profileMetadata.DefaultProfileName = activeProfile.Name;
        SaveProfilesMetadata();
        PopUpController.Instance.QuickNoticeWindow(activeProfile + " set as default!\nThis will be the patch that loads on startup.");
    }

    private void DeleteProfileAndUpdateUi(ProfileSaveData profile)
    {
        if (!DeleteProfile(profile)) 
            return;

        if (!ProfileButtons.Remove(profile, out var ui))
        {
            Debug.LogError($"Failed to remove {profile.Name} from profile buttons", this);
            return;
        }
        
        ui.Annihilate();

        if (ControlsManager.ActiveProfile == profile)
        {
            SetActiveProfile(GetOrUpdateDefaultProfile());
        }
    }


    private bool DeleteProfile(ProfileSaveData profile)
    {
        if(profile.IsBuiltInDefault)
        {
            PopUpController.Instance.ErrorWindow("Can't delete default profile");
            return false;
        }

        //delete file
        var success = !FileHandler.DeleteFile(_basePath, profile.Name, ControlsExtension);
        if(!success)
        {
            PopUpController.Instance.ErrorWindow($"Failed to delete {profile.Name}");
        }

        // remove as default regardless of success
        if (_profileMetadata.DefaultProfileName == profile.Name)
        {
            _profileMetadata.DefaultProfileName = DefaultSaveName;
            SaveProfilesMetadata();
        }

        return success;
    }

    private void ToggleProfileWindow()
    {
        _profileWindow.SetActive(!_profileWindow.activeSelf);
	}

    private void OnSpanWidthToggled(bool e)
    {
        
    }

    private static bool SaveProfile(ProfileSaveData saveData)
    {
        var profileName = saveData.Name;
        if (saveData.IsBuiltInDefault)
        {
            PopUpController.Instance.ErrorWindow("Can't overwrite defaults, use Save As instead in the Profiles page.");
            return false;
        }
        
        var success = FileHandler.SaveJsonObject(saveData, _basePath, profileName, ControlsExtension);

        if (success)
        {
            PopUpController.Instance.QuickNoticeWindow($"Saved {profileName} successfully!");
        }
        else
        {
            PopUpController.Instance.ErrorWindow($"Error saving profile {profileName}. Check the Log for more details.");
        }

        return success;
    }

    private static bool SaveProfileAs(string profileName, [NotNullWhen(true)] out ProfileSaveData newProfile)
    {
        if (profileName == DefaultSaveName || ProfileButtons.Keys.Any(x => x.Name == profileName))
        {
            PopUpController.Instance.ErrorWindow("Profile with this name already exists, please use another.");
            newProfile = null;
            return false;
        }

        var invalidChars = FileHandler.GetInvalidFileNameCharactersIn(profileName);
        if (invalidChars.Count > 0)
        {
            PopUpController.Instance.ErrorWindow(invalidChars.Count == 1
                ? $"Chosen profile name contains an invalid character."
                : $"Chosen profile name contains {invalidChars.Count} invalid characters.");
            newProfile = null;
            return false;
        }

        if (profileName.Length > 0)
        {
            //add this profile to  working profiles in profile selection ui
            //switch to this profile
            var profile = new ProfileSaveData(ControlsManager.ActiveProfile.AllControllers, profileName, ControlsManager.ActiveProfile.SpanWidth);
            var saved = SaveProfile(profile);

            if (!saved)
            {
                newProfile = null;
                return false;
            }
            newProfile = profile;
            return true;
        }

        PopUpController.Instance.ErrorWindow("Please enter a name.");
        newProfile = null;
        return false;
    }

    private bool TryLoadProfile(string fileNameSansExtension, [NotNullWhen(true)] out ProfileSaveData loadedData)
    {
        if (fileNameSansExtension == DefaultSaveName)
        {
            loadedData = null;
            return false;
        }
        
        loadedData = FileHandler.LoadJsonObject<ProfileSaveData>(_basePath, fileNameSansExtension, ControlsExtension);
        return loadedData != null;
    }
    
    private static ProfilesMetadata LoadProfileMetadata()
    {
        var loaded = FileHandler.LoadJsonObject<ProfilesMetadata>(_basePath, ProfileNameSaveName, MetadataExtension);
        return loaded ?? new ProfilesMetadata();
    }

    private static void SaveProfilesMetadata()
    {
        FileHandler.SaveJsonObject(_profileMetadata, _basePath, ProfileNameSaveName, MetadataExtension);
    }

    [Serializable]
    private class ProfilesMetadata
    {
        [SerializeField] private string _defaultProfileName = DefaultSaveName;

        public string DefaultProfileName
        {
            get => _defaultProfileName;
            set => _defaultProfileName = value;
        }
    }
}

[Serializable]
public class ProfileSaveData
{
    [FormerlySerializedAs("_profileName")] [FormerlySerializedAs("name")] [SerializeField]
    private string _name = string.Empty;

    [SerializeField] private bool _spanWidth = false;

    //each type of control data has to be split into its own type-specific list for JsonUtility to agree with it
    [FormerlySerializedAs("faderData")] [SerializeField]
    private List<FaderData> _faderData = new();
    [FormerlySerializedAs("controller2DData")] [SerializeField]
    private List<Controller2DData> _controller2DData = new();
        
    public int ControllerCount => _faderData.Count + _controller2DData.Count;
        
    public bool IsBuiltInDefault => _name == ProfilesManager.DefaultSaveName;

    
    public ProfileSaveData(IEnumerable<ControllerData> controllerData, string name, bool spanWidth)
    {
        foreach (var data in controllerData)
        {
            AddController(data);
        }

        _name = name;
    }

    public void AddController(ControllerData data)
    {
        switch (data)
        {
            case FaderData fader:
                _faderData.Add(fader);
                break;
            case Controller2DData control2D:
                _controller2DData.Add(control2D);
                break;
            default:
                Debug.LogError($"Profile save data does not handle controller type {data.GetType()}");
                break;
        }
    }

    [NonSerialized]
    private readonly List<ControllerData> _allControllers = new();
    public IReadOnlyList<ControllerData> AllControllers
    {
        get
        {
            // todo - optimize
            _allControllers.Clear();
            for(int i = 0; i < _faderData.Count; i++)
            {
                _allControllers.Add(_faderData[i]);
            }
            for(int i = 0; i < _controller2DData.Count; i++)
            {
                _allControllers.Add(_controller2DData[i]);
            }

            return _allControllers;
        }
    }

    public string Name => _name;

    public bool SpanWidth => _spanWidth;

    public bool RemoveController(ControllerData config)
    {
        switch (config)
        {
            case FaderData fader:
                return _faderData.Remove(fader);
            case Controller2DData control2D:
                return _controller2DData.Remove(control2D);
            default:
                Debug.LogError($"Profile save data does not handle controller type {config.GetType()}");
                return false;
        }
    }
}
