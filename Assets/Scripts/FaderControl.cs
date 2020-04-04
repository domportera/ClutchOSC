﻿using System.Collections;
using System.Collections.Generic;
using OscJack;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(OscPropertySender))]
[RequireComponent(typeof(Slider))]
public class FaderControl : MonoBehaviour
{

    [SerializeField] Text label = null;

    OscPropertySender sender = null;
    Slider slider = null;

    ControllerSettings myController = null;
    
    float modValue;
    float pModValue;
    float targetModValue;
    float startingPoint;

    const int FRAMES_TO_SEND_DUPLICATES = 10;
    int dupeCount = FRAMES_TO_SEND_DUPLICATES; //so it doesnt send anything out before it's touched

    AnimationCurve valueCurve;

    public void Initialize(ControllerSettings _controller, ValueCurve[] _curves)
    {
        slider = GetComponent<Slider>();
        sender = GetComponent<OscPropertySender>();
        myController = _controller;

        sender.SetAddress(myController.GetAddress());
        label.text = myController.name;
        name = myController.name + " " + myController.controlType;

        //load default values
        int defaultValue = myController.defaultValue;
        modValue = defaultValue;
        pModValue = defaultValue;
        targetModValue = defaultValue;

        slider.maxValue = myController.max;
        slider.minValue = myController.min;
        
        if(_curves == null || _curves.Length < System.Enum.GetValues(typeof(CurveType)).Length)
        {
            Debug.LogError($"Not all curves are present! Null? {_curves == null }");
        }

        foreach(ValueCurve c in _curves)
        {
            if(c.curveType == myController.curveType)
            {
                valueCurve = c.curve;
                break;
            }
        }

        slider.SetValueWithoutNotify(MapValueToCurve(defaultValue));
    }

    // Update is called once per frame
    void Update()
    {
        if(myController == null)
        {
            Debug.LogError("Null controller on " + gameObject.name);
            return;
        }

        TweenModValue();

        if (modValue == pModValue)
        {
            dupeCount++;

            if (dupeCount > FRAMES_TO_SEND_DUPLICATES) //prevent int overflow
            {
                dupeCount = FRAMES_TO_SEND_DUPLICATES;
            }
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

    public void StartSliding()
    {
        
    }

    public void EndSliding()
    {

        if(myController.controlType == ControlType.Wheel)
        {
            targetModValue = MapValueToCurve(myController.defaultValue);
        }
    }

    public void SetPitch(float _val)
    {
        targetModValue = _val;
    }

    void TweenModValue()
    {
        pModValue = modValue;

        if (modValue == targetModValue)
        {
            return;
        }

        if (myController.smoothTime <= 0)
        {
            modValue = targetModValue;
        }
        else
        {
            float difference = (slider.maxValue - slider.minValue) * Time.deltaTime / myController.smoothTime;

            //set to idle if close enough to zero
            if (Mathf.Abs(modValue - targetModValue) < difference)
            {
                modValue = targetModValue;
            }
            else
            {
                //approach zerovalue
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

        slider.SetValueWithoutNotify(modValue);
    }

    void SetConnected()
    {
        IPSetter.SetConnected();
    }

    void InvalidClient()
    {
        IPSetter.InvalidClient();
    }

    void SendModValue()
    {
        if (IPSetter.IsConnected())
        {
            sender.Send(MapValueToCurve(modValue));
        }
    }

    int MapValueToCurve(float _value)
    {
        if (myController.curveType != CurveType.Linear)
        {
            int range = myController.GetRange();
            float tempVal = _value - myController.min;
            float ratio = tempVal / range;
            float mappedRatio = valueCurve.Evaluate(ratio);

            return (int)(mappedRatio * range + myController.min);
        }
        else
        {
            return (int)_value;
        }
        
    }
}