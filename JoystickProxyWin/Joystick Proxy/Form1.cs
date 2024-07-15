﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using IniParser;
using IniParser.Model;
using SharpDX.DirectInput;

namespace Joystick_Proxy
{
    public partial class Form1 : Form
    {
        private static bool _debug = false;

        private DirectInput _directInput = new DirectInput();

        private BindingList<ControllerDevice> _devices;
        private BindingList<ControllerInput> _input;
        //private BindingList<ControllerDevice> _alterDevices;

        private Socket _socket;
        private IPEndPoint _endPoint;

        private StreamWriter logFileStream = null;

        private static Dictionary<string, string> SupportedDevices = new Dictionary<string, string>();

        public Form1()
        {
            LoadConfig();

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            UpdateEndpoint(Properties.Settings.Default.Host, Properties.Settings.Default.Port);

            _devices = new BindingList<ControllerDevice>();
            _input = new BindingList<ControllerInput>();
            //_alterDevices = new BindingList<ControllerDevice>();

            InitializeComponent();

            ControllerDeviceBindingSource.DataSource = _devices;
            InputBindingSource.DataSource = _input;

            VisualizerHostTextBox.Text = Properties.Settings.Default.Host;
            PortInput.Value = Properties.Settings.Default.Port;

            ScanJoysticks();

            DataGridViewComboBoxColumn modelColumn = new DataGridViewComboBoxColumn();
            modelColumn.HeaderText = "Model";
            modelColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            modelColumn.Name = "ModelComboBoxColumn";
            modelColumn.FillWeight = 30F;
            modelColumn.Width = 300;
            modelColumn.DisplayMember = "Device_Name";
            modelColumn.ValueMember = "Device_USB_ID";
            modelColumn.ReadOnly = false;
            DevicesDataGridView.Columns.Add(modelColumn);

            DevicesDataGridView.RowsAdded += UpdateAlterModelsHandler;
            DevicesDataGridView.RowsRemoved += UpdateAlterModelsHandler;
            DevicesDataGridView.EditingControlShowing += EditingControlShowingHandler;
        }

        private void UpdateAlterModelsHandler(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            UpdateAlterModels();
        }

        private void UpdateAlterModelsHandler(object sender, DataGridViewRowsAddedEventArgs e)
        {
            UpdateAlterModels();
        }

        private void UpdateAlterModels()
        {
            foreach (DataGridViewRow row in DevicesDataGridView.Rows)
            {
                DataGridViewComboBoxCell comboBoxCell = (DataGridViewComboBoxCell)row.Cells["ModelComboBoxColumn"];
                ControllerDevice cellDevice = (ControllerDevice)row.DataBoundItem;

                comboBoxCell.DataSource = cellDevice.AlterModels.Select(m => new { Device_USB_ID = m.Key, Device_Name = m.Value }).ToList();

                if (cellDevice.AlterModels.Any(m => m.Key == cellDevice.UsbId))
                {
                    comboBoxCell.Value = cellDevice.UsbId;
                }
                else
                {
                    comboBoxCell.Value = null;
                }
            }
        }
        private void EditingControlShowingHandler(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (DevicesDataGridView.CurrentCell.ColumnIndex == DevicesDataGridView.Columns["ModelComboBoxColumn"].Index && 
                e.Control is ComboBox comboBox)
            {
                // Remove the existing event handler to avoid duplicate calls
                comboBox.SelectedIndexChanged -= ModelColumn_SelectedIndexChangedHandler;
                // Attach the event handler
                comboBox.SelectedIndexChanged += ModelColumn_SelectedIndexChangedHandler;
            }
        }

        private void ModelColumn_SelectedIndexChangedHandler(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                DataGridViewComboBoxEditingControl editingControl = comboBox as DataGridViewComboBoxEditingControl;
                DataGridView dataGridView = editingControl.EditingControlDataGridView;
                int rowIndex = dataGridView.CurrentCell.RowIndex;
                ControllerDevice cellDevice = (ControllerDevice)dataGridView.Rows[rowIndex].DataBoundItem;

                if (comboBox.SelectedValue != null)
                {
                    cellDevice.SelectedModelUsbId = comboBox.SelectedValue.ToString();
                }
            }
        }

        private void UpdateEndpoint(string host, int port)
        {
            IPAddress hostIp = IPAddress.Loopback;
            foreach(IPAddress ip in Dns.GetHostAddresses(host))
            {
                if(ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    hostIp = ip;
                    break;
                }
            }

            if(_endPoint == null)
            {
                _endPoint = new IPEndPoint(hostIp, port);
            } else
            {
                _endPoint.Address = hostIp;
                _endPoint.Port = port;
            }
        }

        private static void LoadConfig()
        {
            var parser = new FileIniDataParser();
            IniData data = parser.ReadFile("settings.ini");

            foreach (KeyData supportedDevice in data["Devices"])
            {
                if (supportedDevice.KeyName.StartsWith("#"))
                {
                    continue;
                }

                SupportedDevices.Add(supportedDevice.KeyName, supportedDevice.Value);
                Debug(" * " + supportedDevice.Value);
            }
        }

        private void ScanJoysticks()
        {
            List<ControllerDevice> removedDevices = new List<ControllerDevice>();
            List<ControllerDevice> addedDevices = new List<ControllerDevice>(); //_directInput.GetDevices().ToList().ConvertAll(device => new ControllerDevice(_directInput, device));

            List<DeviceInstance> oldDeviceInstances = _devices.ToList().ConvertAll(d => d.DeviceInstance);
            List<DeviceInstance> foundDeviceInstances = _directInput.GetDevices().ToList();

            //List<DeviceInstance> removedDeviceInstances = oldDeviceInstances.Except(foundDeviceInstances).Where(d => !IsSupported(d)).ToList();

            foreach(DeviceInstance deviceInstance in foundDeviceInstances)
            {
                if (_devices.Where(d => d.DeviceInstance.InstanceGuid == deviceInstance.InstanceGuid).Count() == 0)
                {
                    if (ShowAllDevicesCheckBox.Checked == true || SupportedDevices.ContainsKey(ControllerDevice.ProductGuidToUSBID(deviceInstance.ProductGuid)))
                    {
                        Dictionary<string, string>  alterModels = UpdateModelComboBoxes(deviceInstance);
                        addedDevices.Add(new ControllerDevice(_directInput, deviceInstance, alterModels));
                    }
                }
            }
            
            foreach(ControllerDevice device in _devices)
            {
                bool match = false;
                if (SupportedDevices.ContainsKey(device.UsbId) || ShowAllDevicesCheckBox.Checked)
                {
                    match = foundDeviceInstances.ConvertAll(d => d.InstanceGuid.ToString()).Contains(device.Guid);
                }
                if(!match)
                {
                    // Remove device
                    removedDevices.Add(device);
                }
            }
            
            foreach(ControllerDevice device in removedDevices)
            {
                RemoveDevice(device);
            }

            foreach (ControllerDevice device in addedDevices)
            {
                AddDevice(device);
            }
        }

        private Dictionary<string, string> UpdateModelComboBoxes(DeviceInstance deviceInstance)
        {
            Dictionary<string, string> alterModels = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> devices in SupportedDevices)
            {
                string usb_id = Regex.Replace(deviceInstance.ProductGuid.ToString(), @"(^....)(....).*$", "$2:$1");

                if (!ModelFilterCheckBox.Checked ||
                    (ModelFilterCheckBox.Checked && 
                    SupportedDevices.ContainsKey(usb_id) &&                     
                    ((devices.Value.ToLower().Contains("joystick") && SupportedDevices[usb_id].ToLower().Contains("joystick")) ||
                    (devices.Value.ToLower().Contains("throttle") && SupportedDevices[usb_id].ToLower().Contains("throttle")) ||
                    (devices.Value.ToLower().Contains("hotas") && SupportedDevices[usb_id].ToLower().Contains("hotas")) ||
                    (devices.Value.ToLower().Contains("pedals") && SupportedDevices[usb_id].ToLower().Contains("pedals")))))
                {
                    alterModels.Add(devices.Key, devices.Value);
                }
            }

            return alterModels;
        }

        private bool IsSupported(DeviceInstance deviceInstance)
        {
            return IsSupported(ControllerDevice.ProductGuidToUSBID(deviceInstance.ProductGuid));
        }

        private bool IsSupported(string usbId)
        {
            return SupportedDevices.ContainsKey(usbId);
        }

        private void AddDevice(ControllerDevice addedDevice)
        {
            if (SupportedDevices.ContainsKey(addedDevice.UsbId))
            {
                addedDevice.OnStateUpdated += Device_OnStateUpdated;
                addedDevice.Supported = true;
                addedDevice.Enabled = true;
            }
            addedDevice.Acquire();
            _devices.Add(addedDevice);
            SendEvent(addedDevice, "Connected=1");
        }

        private void RemoveDevice(ControllerDevice removedDevice)
        {
            removedDevice.Unacquire();
            _devices.Remove(removedDevice);
            SendEvent(removedDevice, "Connected=0");
        }

        private void Device_OnStateUpdated(object sender, DeviceStateUpdateEventArgs e)
        {
            Debug("State updated for " + e.Device.Name + " with " + e.UpdatedStates.Count + " events");
            SendEvents(e.Device, e.UpdatedStates.Select(ev => ev.ToString()).ToList());
        }

        private void SendEvents(ControllerDevice device, List<string> events)
        {
            SendEvent(device, String.Join(",", events));
        }

        private void SendEvent(ControllerDevice device, string e)
        {
            try
            {
                if (_socket == null || _endPoint == null)
                    return;

                bool supportedDevice = SupportedDevices.ContainsKey(device.UsbId);
                string id = device.UsbId;
                if (id == "046d:c212")
                {
                    id = "046d:c215";
                }

                string outgoingString = String.Format("{0},{1},{2}", device.SelectedModelUsbId, device.AlterModels[device.SelectedModelUsbId], e);

                if (supportedDevice)
                {
                    byte[] send_buffer = Encoding.ASCII.GetBytes(outgoingString);
                    _socket.SendTo(send_buffer, _endPoint);
                }

                if (logFileStream != null)
                {
                    double timestamp = DateTime.UtcNow.ToUniversalTime()
                        .Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                        .TotalMilliseconds;
                    logFileStream.WriteLine(Math.Round(timestamp) + "," + outgoingString);
                }

                Debug(outgoingString);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static void Debug(string outgoingString)
        {
            if (_debug)
            {
                Console.WriteLine(outgoingString);
            }
        }

        private void RefreshDevicesTimer_Tick(object sender, System.EventArgs e)
        {
            ControllerDevice selectedItem = null;
            int selectedCell = 0;

            if (DevicesDataGridView.SelectedCells.Count > 0)
            {
                selectedItem = (ControllerDevice)DevicesDataGridView.SelectedCells[0].OwningRow.DataBoundItem;
                selectedCell = DevicesDataGridView.SelectedCells[0].ColumnIndex;
            }

            ScanJoysticks();

            foreach (DataGridViewRow row in DevicesDataGridView.Rows)
            {
                ControllerDevice rowObject = (ControllerDevice)row.DataBoundItem;

                if (selectedItem != null && rowObject == selectedItem)
                {
                    DevicesDataGridView.ClearSelection();
                    row.Cells[selectedCell].Selected = true;
                    break;
                }
            }
        }

        private void DevicesDataGridView_SelectionChanged(object sender, EventArgs e)
        {
            ReadInputTimer.Enabled = DevicesDataGridView.CurrentRow != null;
        }

        private void ReadInputTimer_Tick(object sender, EventArgs e)
        {
            foreach(ControllerDevice device in _devices)
            {
                try { device.Update(); } catch(Exception ex) {
                    Debug("Failure when running device Update()");
                    Debug(ex.Message);
                }
            }

            ControllerDevice selectedDevice = (ControllerDevice)DevicesDataGridView.CurrentRow.DataBoundItem;

            _input.Clear();
            foreach (JoystickUpdate inputState in selectedDevice.CurrentState.Values)
            {
                _input.Add(new ControllerInput(inputState));
            }
        }

        private void DevicesDataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            foreach (DataGridViewRow row in DevicesDataGridView.Rows)
            {
                ControllerDevice cd = (ControllerDevice)row.DataBoundItem;
                if(!SupportedDevices.ContainsKey(cd.UsbId))
                {
                    row.DefaultCellStyle.BackColor = Color.LightGray;
                    //row.Cells[0].ReadOnly = true;
                }
            }
        }

        private void DevicesDataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 0 && e.RowIndex >= 0)
            {
                DevicesDataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
                bool enabled = (bool)this.DevicesDataGridView.CurrentCell.Value == true;
                this.DevicesDataGridView.Rows[e.RowIndex].Cells[0].Value = enabled;

                ControllerDevice selectedDevice = (ControllerDevice)this.DevicesDataGridView.Rows[e.RowIndex].DataBoundItem;
                if (enabled)
                    selectedDevice.OnStateUpdated += Device_OnStateUpdated;
                else
                    selectedDevice.OnStateUpdated -= Device_OnStateUpdated;

            }
        }

        private void ShowAllDevicesCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            RefreshDevicesTimer_Tick(sender, e);
        }

        private void ModelFilterCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in DevicesDataGridView.Rows)
            {
                ControllerDevice cellDevice = (ControllerDevice)row.DataBoundItem;
                cellDevice.AlterModels = UpdateModelComboBoxes(cellDevice.DeviceInstance);
                UpdateAlterModels();
            }
        }

        private void VisualizerHostTextBox_Leave(object sender, EventArgs e)
        {
            Properties.Settings.Default.Host = VisualizerHostTextBox.Text;
            Properties.Settings.Default.Save();
            UpdateEndpoint(VisualizerHostTextBox.Text, Properties.Settings.Default.Port);
        }

        private void VisualizerHostTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if(e.KeyChar == (char)Keys.Enter)
            {
                this.ActiveControl = null;
            }
        }

        private void LogToFileCheckbox_Click(object sender, EventArgs e)
        {
            if (LogToFileCheckbox.Checked)
            {
                if (SaveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    if (SaveFileDialog.FileName != "")
                    {
                        logFileStream = new StreamWriter(SaveFileDialog.FileName);
                    }
                }
                else
                {
                    if (logFileStream != null)
                        logFileStream.Close();
                    LogToFileCheckbox.Checked = false;
                }
            }
            else
            {
                if (logFileStream != null)
                    logFileStream.Close();

                logFileStream = null;
            }
        }

        private void SavePollRate()
        {
            Properties.Settings.Default.PollingRate = (int)PollingRateInput.Value;
            ReadInputTimer.Interval = (int)PollingRateInput.Value;
            Properties.Settings.Default.Save();
        }

        private void PollingRateInput_ValueChanged(object sender, EventArgs e)
        {
            SavePollRate();
        }

        private void PollingRateInput_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                ActiveControl = null;
            }
        }

        private void PollingRateInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ActiveControl = null;
            }
        }

        private void PortInput_ValueChanged(object sender, EventArgs e)
        {
            var port = Convert.ToInt32(PortInput.Value);
            Properties.Settings.Default.Port = port;
            Properties.Settings.Default.Save();
            UpdateEndpoint(VisualizerHostTextBox.Text, port);
        }
    }
}
