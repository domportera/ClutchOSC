﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] GameObject optionsPanel = null;
    [SerializeField] GameObject sliderOptionsButtonLayoutPrefab = null;
    [SerializeField] GameObject faderOptionsPrefab = null;
    [SerializeField] GameObject faderOptionsActivationPrefab = null; //the prefab for the button that opens up fader options
    [SerializeField] GameObject sliderButtonVerticalLayoutParent = null;

    const int sliderButtonLayoutCapacity = 5;

    List<ControllerButtonGroup> controllerButtons = new List<ControllerButtonGroup>();
    List<LayoutGroupButtonCount> layoutCounts = new List<LayoutGroupButtonCount>();
    List<GameObject> sliderOptionsButtonLayoutGroups = new List<GameObject>();

    // Start is called before the first frame update
    void Start()
    {
        optionsPanel.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //used by options button in scene
    public void ToggleOptionsMenu()
    {
        optionsPanel.SetActive(!optionsPanel.activeInHierarchy);
    }

    public void SpawnFaderOptions(ControllerSettings _config, GameObject _control)
    {
        //check if any other controller buttons exist for this, then destroy all its contents
        //make sure to destroy faderOptions as well
        ControllerButtonGroup dupe = GetButtonGroupByConfig(_config);
        if(dupe != null)
        {
            DestroyControllerGroup(dupe);
        }

        ControllerButtonGroup buttonGroup = new ControllerButtonGroup(_config, faderOptionsPrefab, faderOptionsActivationPrefab, _control);

        buttonGroup.faderOptions.transform.SetParent(optionsPanel.transform, false);
        controllerButtons.Add(buttonGroup);

        SortOptionsButtons();
    }

    void AddOptionsButtonToLayout(GameObject _button)
    {
        for(int i = 0; i < layoutCounts.Count; i++)
        {
            if(layoutCounts[i].count < sliderButtonLayoutCapacity)
            {
                _button.transform.SetParent(layoutCounts[i].layoutGroup.transform);
                _button.transform.SetSiblingIndex(layoutCounts[i].count);
                layoutCounts[i].count++;
                return;
            }
        }

        //all layouts full, create a new one

        AddNewLayoutCount();
        AddOptionsButtonToLayout(_button);
    }

    void DestroyControllerGroup(ControllerButtonGroup _buttonGroup)
    {
        //destroy all params
        _buttonGroup.SelfDestruct();

        //remove from list
        controllerButtons.Remove(_buttonGroup);

        //check for empty layouts, and destroy it
        DestroyEmptyLayouts();

        SortOptionsButtons();
    }

    void DestroyEmptyLayouts()
    {
        for(int i = 0; i < layoutCounts.Count; i++)
        {
            if(layoutCounts[i].count == 0)
            {
                //destroy
                Destroy(layoutCounts[i].layoutGroup);
                layoutCounts.RemoveAt(i);
                i--;
            }
        }
    }

    public void DestroyControllerObjects(ControllerSettings _config)
    {
        ControllerButtonGroup buttonGroup = GetButtonGroupByConfig(_config);
        DestroyControllerGroup(buttonGroup);
    }

    void RemoveFromLayout(ControllerButtonGroup _buttonGroup)
    {
        LayoutGroupButtonCount layout = GetLayoutGroupFromObject(_buttonGroup.faderMenuButton.gameObject);

        if (layout != null)
        {
            layout.count--;
        }
        else
        {
            Debug.LogError("Null layout! button didn't find its parent.");
        }
    }

    void AddNewLayoutCount()
    {
        LayoutGroupButtonCount lay = new LayoutGroupButtonCount();
        lay.layoutGroup = Instantiate(sliderOptionsButtonLayoutPrefab);
        lay.layoutGroup.transform.SetParent(sliderButtonVerticalLayoutParent.transform);
        layoutCounts.Add(lay);
    }

    bool GetControllerEnabled(FaderOptions _faderOptions)
    {
        ControllerButtonGroup group = GetButtonGroupByFaderOptions(_faderOptions);

        if(group.activationToggle.isOn)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    ControllerButtonGroup GetButtonGroupByConfig(ControllerSettings _config)
    {
        foreach (ControllerButtonGroup cbg in controllerButtons)
        {
            if (cbg.controllerConfig == _config)
            {
                return cbg;
            }
        }
        return null;
    }

    ControllerButtonGroup GetButtonGroupByFaderOptions(FaderOptions _faderOptions)
    {
        foreach (ControllerButtonGroup cbg in controllerButtons)
        {
            if(cbg.faderOptions == _faderOptions)
            {
                return cbg;
            }
        }

        Debug.LogError("Didn't find a match for button group! Returning empty.");
        return null;
    }

    LayoutGroupButtonCount GetLayoutGroupFromObject(GameObject _button)
    {
        foreach(LayoutGroupButtonCount lay in layoutCounts)
        {
            Transform[] children = lay.layoutGroup.GetComponentsInChildren<Transform>();

            foreach(Transform child in children)
            {
                if(child.gameObject == _button)
                {
                    return lay;
                }
            }
        }

        return null;
    }

    void SortOptionsButtons()
    {
        foreach(LayoutGroupButtonCount layCount in layoutCounts)
        {
            layCount.count = 0;
        }

        //get all the unnamed ones out of sorting list
        List<ControllerButtonGroup> unnamedControls = new List<ControllerButtonGroup>();
        for (int i = 0; i < controllerButtons.Count; i++)
        {
            if(controllerButtons[i].controllerConfig.name == ControlsManager.newControllerName)
            {
                unnamedControls.Add(controllerButtons[i]);
                controllerButtons.Remove(controllerButtons[i]);
                i--;
            }
        }

        //sort buttons
        controllerButtons.Sort((s1, s2) => s1.controllerConfig.name.CompareTo(s2.controllerConfig.name));

        //add unnamed ones to the end
        foreach (ControllerButtonGroup c in unnamedControls)
        {
            controllerButtons.Add(c);
        }

        //place buttons
        foreach (ControllerButtonGroup c in controllerButtons)
        {
            AddOptionsButtonToLayout(c.faderMenuButton.gameObject);
        }

    }

    class LayoutGroupButtonCount
    {
        public GameObject layoutGroup;
        public int count = 0;
    }

    class ControllerButtonGroup
    {
        //should be public get private set
        public FaderOptions faderOptions;
        public Button faderMenuButton;
        public ControllerSettings controllerConfig;
        public Toggle activationToggle;
        public GameObject controlObject;

        public ControllerButtonGroup(ControllerSettings _config, GameObject _faderOptionsPrefab, GameObject _optionsActivateButtonPrefab, GameObject _controlObject)
        {
            faderOptions = Instantiate(_faderOptionsPrefab).GetComponent<FaderOptions>();
            faderOptions.gameObject.name = _config.name + " Options Panel";
            faderOptions.controllerConfig = _config;

            controllerConfig = _config;

            faderMenuButton = Instantiate(_optionsActivateButtonPrefab).GetComponent<Button>();
            faderMenuButton.gameObject.name = _config.name + " Options";
            faderMenuButton.GetComponentInChildren<Text>().text = _config.name + " Options"; // change visible button title
            faderMenuButton.onClick.AddListener(DisplayFaderOptions);

            activationToggle = faderMenuButton.GetComponentInChildren<Toggle>();
            activationToggle.onValueChanged.AddListener(ToggleControlVisibility);
            controlObject = _controlObject;
        }

        void DisplayFaderOptions()
        {
            faderOptions.gameObject.SetActive(true);
        }

        void ToggleControlVisibility(bool _b)
        {
            controlObject.SetActive(_b);
        }

        public void SelfDestruct()
        {
            Destroy(faderOptions.gameObject);
            Destroy(faderMenuButton.gameObject);
            Destroy(controlObject);
        }
    }
}