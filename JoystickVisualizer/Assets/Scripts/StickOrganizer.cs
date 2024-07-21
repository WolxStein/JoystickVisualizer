using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StickOrganizer : MonoBehaviour {
    private const float OFFSET = 20.0f;

    public GameObject NoDevicesText;
    public GameObject[] ControllerModels;

    private List<GameObject> activeControllers = new List<GameObject>();

    private GameObject LeftDevice, CenterDevice, RightDevice;

    // Use this for initialization
    void Start() {
        LeftDevice = null;
        CenterDevice = null;
        RightDevice = null;
        if (ControllerModels == null || ControllerModels.Length == 0)
            ControllerModels = GameObject.FindGameObjectsWithTag("ControllerModel").OrderBy(o => o.transform.parent.GetSiblingIndex()).ToArray();
    }
	
	// Update is called once per frame
	void Update () {
        if (ControllerModels == null)
            return;

        int oldLength = activeControllers.Count;

        activeControllers.Clear();

        foreach (GameObject model in ControllerModels)
        {
            if (model.activeInHierarchy)
            {
                if (model != LeftDevice && model != CenterDevice && model != RightDevice)
                {
                    switch (model.transform.parent.tag)
                    {
                        case "LeftDevice":
                            if (LeftDevice != null)
                            {
                                LeftDevice.SetActive(false);
                            }
                            LeftDevice = model;
                            break;

                        case "CenterDevice":
                            if (CenterDevice != null)
                            {
                                CenterDevice.SetActive(false);
                            }
                            CenterDevice = model;
                            break;

                        case "RightDevice":
                            if (RightDevice != null)
                            {
                                RightDevice.SetActive(false);
                            }
                            RightDevice = model;
                            break;
                    }
                }

                activeControllers.Add(model);
                NoDevicesText.SetActive(activeControllers.Count == 0);

                Debug.Log("Active controller count changed from " + oldLength + " to " + activeControllers.Count + ", reorganizing models");

                float center = ((activeControllers.Count - 1) * OFFSET) / 2;

                for (int i = 0; i < activeControllers.Count; i++)
                {
                    activeControllers[i].transform.position = new Vector3((i * OFFSET) - center, activeControllers[i].transform.position.y, activeControllers[i].transform.position.z);
                }
            }
        }
    }
}
