﻿#endregion License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using J2534DotNet;
using SAE;
using System.Runtime.InteropServices;

namespace UnlimitedDiag
{
    public partial class Root_Form : Form
    {
        List<J2534PhysicalDevice> PhysicalDevices;
        public Root_Form()
        {
            InitializeComponent();
            PhysicalDevices = J2534Discovery.OpenEverything();
        }

        private void CmdDetectVehicleClick(object sender, EventArgs e)
        {
            IntPtr p = Marshal.AllocHGlobal(25);

            byte[] b = new byte[25];
            Marshal.StructureToPtr<byte[]>(b, p, false);
            Marshal.FreeHGlobal(p);
            return;


            if (!PhysicalDevices.Any())
                return;
            if (!PhysicalDevices[0].IsConnected)
                return;

            Channel Ch = PhysicalDevices[0].ConstructChannel(J2534PROTOCOL.ISO15765, J2534BAUD.ISO15765, J2534CONNECTFLAG.NONE);

            if (Ch == null)
                return;

            Ch.StartMsgFilter(new MessageFilter(COMMONFILTER.STANDARDISO15765, new List<byte>{ 0x00, 0x00, 0x07, 0xE0 }));
            Ch.SetConfig(J2534PARAMETER.LOOP_BACK, 0);

            SAE.SAEDiag Diagnostic = new SAE.SAEDiag();

            if (Diagnostic.Ping(Ch))
                MessageBox.Show("We have a successful ping!");
            Ch.Disconnect();

        }

        private void CmdReadVoltageClick(object sender, EventArgs e)
        {
            float voltage = 0;
            if (!PhysicalDevices.Any())
                return;
            if (!PhysicalDevices[0].IsConnected)
                return;
            voltage = (float)PhysicalDevices[0].MeasureBatteryVoltage() / 1000;
            txtVoltage.Text = voltage.ToString("F3") + "v";
        }

        private void CmdReadVinClick(object sender, EventArgs e)
        {
            //txtReadVin.Text = vin;
        }
    }
}
