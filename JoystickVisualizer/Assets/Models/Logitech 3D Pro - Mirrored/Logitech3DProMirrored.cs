﻿using Assets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Logitech3DPro: MonoBehaviour {
    public const string USB_ID = "046d:c215";
    //public const string USB_ID = "044f:0402";
    //public const string USB_ID = "044f:0404";

    public GameObject Model;
    public GameObject Joystick;
    public GameObject Throttle;

    // Use this for initialization
    void Start()
    {
        UDPListener.StickEventListener += StickEvent;
    }

    // Update is called once per frame
    void Update()
    {
    }

    void StickEvent(JoystickState state)
    {
        if (state.UsbID != USB_ID)
        {
            return;
        }

        Model.SetActive(true);

        foreach (KeyValuePair<string, int> entry in state.Data)
        {
            switch (entry.Key)
            {
                case "Connected":
                    if (Model.activeInHierarchy)
                        Model.SetActive(entry.Value == 1);
                    break;

                case "X":
                    Joystick.transform.localEulerAngles = new Vector3(Joystick.transform.localEulerAngles.x, ConvertRange(entry.Value, 0, 65535, -20, 20), Joystick.transform.localEulerAngles.z);
                    break;
                case "Y":
                    Joystick.transform.localEulerAngles = new Vector3(ConvertRange(entry.Value, 0, 65535, 20, -20), Joystick.transform.localEulerAngles.y, Joystick.transform.localEulerAngles.z);
                    break;

                case "RotationZ":
                    Joystick.transform.localEulerAngles = new Vector3(Joystick.transform.localEulerAngles.x, Joystick.transform.localEulerAngles.y, ConvertRange(entry.Value, 0, 65535, -20, 20));
                    break;

                case "Sliders0":
                    Throttle.transform.localEulerAngles = new Vector3(ConvertRange(entry.Value, 0, 65535, 30, -85), Throttle.transform.localEulerAngles.y, Throttle.transform.localEulerAngles.z);
                    break;
            }
        }
    }

    public static float ConvertRange(
        double value, // value to convert
        double originalStart, double originalEnd, // original range
        double newStart, double newEnd) // desired range
    {
        double scale = (double)(newEnd - newStart) / (originalEnd - originalStart);
        return (float)(newStart + ((value - originalStart) * scale));
    }

}
