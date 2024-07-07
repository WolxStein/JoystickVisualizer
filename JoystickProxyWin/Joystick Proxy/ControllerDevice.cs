﻿using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Joystick_Proxy
{
    class ControllerDevice : IEquatable<ControllerDevice>
    {
        public delegate void DeviceStateUpdateHandler(object sender, DeviceStateUpdateEventArgs e);
        public event DeviceStateUpdateHandler OnStateUpdated;

        public string Name { get { return _deviceInstance.InstanceName; } }
        public string Guid { get { return _deviceInstance.InstanceGuid.ToString();  } }
        public string UsbId { get => _usbId; }
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;

                UpdateState(new List<ControllerInput> { new ControllerInput(_enabled) } );
            }
        }

        public Joystick Joystick { get => _joystick; set => _joystick = value; }
        public SortedDictionary<string, JoystickUpdate> CurrentState { get => _inputStateDictionary; }
        private SortedDictionary<string, JoystickUpdate> _inputStateDictionary = new SortedDictionary<string, JoystickUpdate>();

        public DeviceInstance DeviceInstance { get => _deviceInstance; }
        public bool Supported { get; internal set; }
        public Dictionary<string, string> AlterModels { get => _alterModels; }
        public string SelectedModelUsbId { get => _selectedModelUsbId; set => _selectedModelUsbId = value; }

        private DeviceInstance _deviceInstance;
        private Joystick _joystick;
        private string _usbId;

        private bool _enabled = false;
        private bool NotPollable = false;

        private Dictionary<string, string> _alterModels;
        private string _selectedModelUsbId;

        public ControllerDevice(DirectInput di, DeviceInstance deviceInstance, Dictionary<string, string> alterModels = null)
        {
            _deviceInstance = deviceInstance;
            _usbId = ProductGuidToUSBID(_deviceInstance.ProductGuid);
            _joystick = new Joystick(di, deviceInstance.InstanceGuid);
            _alterModels = alterModels;
            _selectedModelUsbId = _usbId;

            Joystick.Properties.BufferSize = 32;
        }

        public static string ProductGuidToUSBID(Guid guid)
        {
            return Regex.Replace(guid.ToString(), @"(^....)(....).*$", "$2:$1");
        }

        public void Update() {
            if (NotPollable || _enabled == false)
                return;

            try
            {
                Joystick.Poll();
            }
            catch (Exception)
            {
                NotPollable = true;
                return;
            }

            List<ControllerInput> updatedStates = new List<ControllerInput>();

            foreach (JoystickUpdate joystickUpdate in Joystick.GetBufferedData())
            {
                _inputStateDictionary[joystickUpdate.Offset.ToString()] = joystickUpdate;
                updatedStates.Add(new ControllerInput(joystickUpdate));
            }

            if(updatedStates.Count > 0)
                UpdateState(updatedStates);
        }

        private void UpdateState(List<ControllerInput> updatedStates)
        {
            // Make sure someone is listening to event
            if (OnStateUpdated == null) return;

            DeviceStateUpdateEventArgs args = new DeviceStateUpdateEventArgs(this, updatedStates);
            OnStateUpdated(this, args);
        }

        public override int GetHashCode()
        {
            return _deviceInstance.InstanceGuid.GetHashCode();
        }

        public bool Equals(ControllerDevice other)
        {
            return _deviceInstance.InstanceGuid == other.DeviceInstance.InstanceGuid;
        }

        internal void Unacquire()
        {
            try { Joystick.Unacquire(); } catch (Exception) { }
        }

        internal void Acquire()
        {
            Joystick.Acquire();
        }
    }

    internal class DeviceStateUpdateEventArgs
    {
        public List<ControllerInput> UpdatedStates { get; set; }
        public ControllerDevice Device { get; set; }

        public DeviceStateUpdateEventArgs(ControllerDevice device, List<ControllerInput> updatedStates)
        {
            this.Device = device;
            this.UpdatedStates = updatedStates;
        }
    }
}
