﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OscJack;
using UnityEngine.UI;

public class Controller : MonoBehaviour
{
    [SerializeField] OscPropertySender oscSender;
    [SerializeField] Button sortLeftButton = null;
    [SerializeField] Button sortRightButton = null;
    public ControllerSettings myController { get; private set; }

    public float modValue { get; private set; }
    protected float pModValue{ get; private set; }
    protected float targetModValue { get; private set; }

    const int FRAMES_TO_SEND_DUPLICATES = 10;
    int dupeCount = FRAMES_TO_SEND_DUPLICATES; //so it doesnt send anything out before it's touched

    // Update is called once per frame
    protected virtual void Update()
    {
        TweenModValue();
        SendValuesWithDuplicates();
    }

    public virtual void Initialize(ControllerSettings _controller)
    {
        if (_controller == null)
        {
            Debug.LogError("Null controller on " + gameObject.name, this);
        }

        myController = _controller;

        oscSender.SetAddress(myController.GetAddress());

        //load default values
        int defaultValue = myController.defaultValue;
        modValue = defaultValue;
        pModValue = defaultValue;
        targetModValue = defaultValue;

        InitializeSorting();
    }

    #region Sorting
    void InitializeSorting()
    {
        if(sortLeftButton == null || sortRightButton == null)
        {
            Debug.LogError($"Sort button null - assign in inspector", this);
            return;
        }

        if (sortLeftButton.onClick.GetPersistentEventCount() == 0)
        {
            sortLeftButton.onClick.AddListener(SortLeft);
            sortRightButton.onClick.AddListener(SortRight);
        }

        SetSortButtonVisibility(false);
    }
    public virtual void SetSortButtonVisibility(bool _visible)
    {
        if (sortLeftButton == null || sortRightButton == null)
        {
            Debug.LogError($"Sort button null - assign in inspector", this);
            return;
        }

        sortLeftButton.gameObject.SetActive(_visible);
        sortRightButton.gameObject.SetActive(_visible);
    }
    void SortLeft()
    {
        SortPosition(false);
    }

    void SortRight()
    {
        SortPosition(true);
    }

    void SortPosition(bool _right)
    {
        transform.SetSiblingIndex(_right ? transform.GetSiblingIndex() + 1 : transform.GetSiblingIndex() - 1);
    }
    #endregion Sorting

    #region Mod Value Manipulation
    protected void SetValue(float _val)
    {
        targetModValue = _val;
    }

    protected void SetValue (int _val)
    {
        targetModValue = _val;
    }

    public void SetValueAsPercentage (float _val)
    {
        targetModValue = Mathf.Clamp01(Mathf.Lerp(myController.min, myController.max, _val));
    }

    protected int MapValueToCurve(float _value, bool _inverse)
    {
        if (myController.curveType != CurveType.Linear)
        {

            int range = myController.GetRange();
            float tempVal = _value - myController.min;
            float ratio = tempVal / range;
            float mappedRatio;

            if (_inverse)
            {
                mappedRatio = myController.curveType == CurveType.Logarithmic ? Mathf.Pow(ratio, 2f) : Mathf.Sqrt(ratio);
            }
            else
            {
                mappedRatio = myController.curveType == CurveType.Logarithmic ? Mathf.Sqrt(ratio) : Mathf.Pow(ratio, 2);
            }

            return (int)(mappedRatio * range + myController.min);
        }
        else
        {
            return (int)_value;
        }

    }

    void TweenModValue()
    {
        pModValue = modValue;

        if (modValue == targetModValue)
        {
            return;
        }

        bool shouldSmooth = myController.smoothTime <= 0;
        if (shouldSmooth)
        {
            modValue = targetModValue;
        }
        else
        {
            float difference = (myController.max - myController.min) * Time.deltaTime / myController.smoothTime;

            //set to idle if close enough to zero
            if (Mathf.Abs(modValue - targetModValue) < difference)
            {
                modValue = targetModValue;
            }
            else
            {
                //approach target value
                if (modValue > targetModValue)
                {
                    modValue -= difference;
                }
                else
                {
                    modValue += difference;
                }
            }
        }
    }

    protected void ReturnToCenter()
    {
        if (myController.controlType == ControlType.ReturnToCenter)
        {
            SetValue(MapValueToCurve(myController.defaultValue, true));
        }
    }
    #endregion Mod Value Manipulation

    #region OSC Communication
    void SendValuesWithDuplicates()
    {
        if (modValue == pModValue)
        {
            dupeCount = Mathf.Clamp(++dupeCount, 0, FRAMES_TO_SEND_DUPLICATES);
        }
        else
        {
            dupeCount = 0;
        }

        if (dupeCount < FRAMES_TO_SEND_DUPLICATES)
        {
            SendModValue();
        }
    }

    void SendModValue()
    {
        if (IPSetter.IsConnected())
        {
            oscSender.Send(MapValueToCurve(modValue, false));
        }
    }

    //SetConnected and InvalidClient are here because the OscPropertySender can't access
    //the IP address on its own, so it instead uses SendMessage.
    //I'm certain there's a smarter way to do it but I'm going to defer to my past self
    //for now until I have a reason to change it
    void SetConnected()
    {
        IPSetter.SetConnected();
    }
    void InvalidClient()
    {
        IPSetter.InvalidClient();
    }
    #endregion OSC Communication
}