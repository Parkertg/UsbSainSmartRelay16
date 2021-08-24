using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Ports;
using System.Threading;
using Common.Communication;

namespace Parkertg.SainSmart
{

    [DisplayName("Serial")]
    [DataContract]
    public class Serial
    {
        #region public properties

        public string PortName => portName;

        public int BaudRate { get; set; }

        public bool IsOpen => serialPort?.IsOpen ?? false;

        public int BytesToRead => serialPort.BytesToRead;

        public bool DtrEnable { get; set; }

        #endregion public properties

        #region protected members
        
        protected SerialPort serialPort;
        protected string portName = string.Empty;
        
        #endregion private members

        public Serial(string serialPortName)
        {
            portName = serialPortName;

            serialPort = new SerialPort(portName);
        }

        public void Open()
        {
            serialPort.BaudRate = BaudRate == 0 ? 115200 : BaudRate;
            serialPort.DtrEnable = DtrEnable;
            serialPort.Open();
        }

        public void Close()
        {
            serialPort.Close();

            serialPort.Dispose();
        }

        public void Dispose()
        {
            serialPort?.Dispose();
        }

        public void WriteBytes(byte[] bytes)
        {
            SerialPortOpenCheck();
            serialPort.WriteTimeout = SerialPort.InfiniteTimeout;
            serialPort.Write(bytes, 0, bytes.Length);
        }

        public void WriteBytes(byte[] bytes, int timeout)
        {
            SerialPortOpenCheck();
            serialPort.WriteTimeout = timeout;
            serialPort.Write(bytes, 0, bytes.Length);
        }

        public byte[] ReadBytes(int timeout)
        {
            bool timeoutFlag = false;

            byte[] readMessage = null;

            var MsgReceiveTimer = new System.Timers.Timer();

            MsgReceiveTimer.Interval = timeout;

            MsgReceiveTimer.Elapsed += (sender, args) => { timeoutFlag = true; };

            MsgReceiveTimer.Start();

            while (readMessage == null)
            {
                readMessage = ReadBytes();

                Thread.Sleep(10);

                if (timeoutFlag)
                {
                    break;
                }
            }

            MsgReceiveTimer.Dispose();

            return readMessage;
        }

        public byte[] ReadBytes()
        {
            SerialPortOpenCheck();

            byte[] returnData = null;

            int count = serialPort.BytesToRead;

            if (count == 0)
            {
                return null;
            }

            returnData = new byte[count];

            int indexCount = 0;

            int intReturnASCII = 0;

            while (count > 0)
            {
                intReturnASCII = serialPort.ReadByte();

                returnData[indexCount] = Convert.ToByte(intReturnASCII);

                count--;

                indexCount++;
            }

            return returnData;
        }

        public static List<string> GetAvailablePorts()
        {
            string[] availablePorts = SerialPort.GetPortNames();

            return new List<string>(availablePorts);
        }

        public bool IsAvailableForUse(out string info)
        {
            info = string.Empty;

            var serial = new Serial(portName);

            try
            {
                serial.Open();

                if (serial.IsOpen)
                {
                    serial.Close();

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                info = e.Message;

                return false;
            }
        }

        private void SerialPortOpenCheck()
        {
            if (!IsOpen)
            {
                throw new Exception(string.Format("Serial port {0} is not open or avaialable", portName));
            }
        }
    }
    
    public enum RelayNum : int
    {
        Chnl1 = 1,
        Chnl2 = 2,
        Chnl3 = 3,
        Chnl4 = 4,
        Chnl5 = 5,
        Chnl6 = 6,
        Chnl7 = 7,
        Chnl8 = 8,
        Chnl9 = 9,
        Chnl10 = 10,
        Chnl11 = 11,
        Chnl12 = 12,
        Chnl13 = 13,
        Chnl14 = 14,
        Chnl15 = 15,
        Chnl16 = 16
    }

    public class SainSmartRelay16 : Serial, IDisposable
    {
        #region Members

        // Arrays used as dictionaries
        static byte[] Convert_Address = new byte[17] { 0x00,0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46 };
        static byte[] T_On_Chk_Value = new byte[17] { 0x00 ,0x45, 0x44, 0x43, 0x42, 0x41, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x46 };
        static byte[] T_Off_Chk_Value = new byte[17] { 0x00, 0x44, 0x43, 0x42, 0x41, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x46, 0x45 };

        // Commands / Templates
        static byte[] Single_Relay_Command_Template = new byte[17] {0x3A,0x46,0x45,0x30,0x35,0x30,0x30,0x30,0x30,0x46,0x46,0x30,0x30,0x46,0x45,0x0D,0x0A};
        static byte[] All_On_Command = new byte[23] { 0x3A, 0x46, 0x45, 0x30, 0x46, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x31, 0x30, 0x30, 0x32, 0x46, 0x46, 0x46, 0x46, 0x45, 0x33, 0x0D, 0x0A};
        static byte[] All_Off_Command = new byte[23] { 0x3A, 0x46, 0x45, 0x30, 0x46, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x31, 0x30, 0x30, 0x32, 0x30, 0x30, 0x30, 0x30, 0x45, 0x31, 0x0D, 0x0A};
        

        #endregion

        public SainSmartRelay16(string portName, int baudRate=9600) : base(portName)
        {
            this.BaudRate = baudRate;
        }

        public void AllRelaysON()
        {
            SendCommand(All_On_Command);
        }

        public void AllRelaysOFF()
        {
            SendCommand(All_Off_Command);
        }

        public void SetRelay(RelayNum relayNum, bool enableRelay)
        {
            SetRelay((int)relayNum, enableRelay);
        }

        public void SetRelay(int relayNum, bool enableRelay)
        {
            if (relayNum<1 | relayNum>16) throw new IndexOutOfRangeException(string.Format("RelayNum must be a num from 1 to 16. was {0}",relayNum));

            SendCommand( BuildRelayCommand(relayNum, enableRelay) );
        }
        

        private byte[] BuildRelayCommand(int relayNum, bool enableRelay)
        {
            var TxBuffer = new byte[Single_Relay_Command_Template.Length];
            Single_Relay_Command_Template.CopyTo(TxBuffer,0);

            TxBuffer[8] = Convert_Address[relayNum];     //Relay Address

            TxBuffer[9]  = (byte) ((enableRelay)? 0x46 : 0x30);    // Control Byte #1 - 0x46 to turn-on, 0x30 to turn-off   
            TxBuffer[10] = (byte) ((enableRelay)? 0x46 : 0x30);    // Control Byte #2 - 0x46 to turn-on, 0x30 to turn-off            

            TxBuffer[14] = enableRelay ? 
                T_On_Chk_Value[relayNum] : T_Off_Chk_Value[relayNum];    //Some kind of reverse order check???

            return TxBuffer;
        }

        public void SendCommand(byte[] txBytes)
        {
            bool localcontrol = false;
            if (!IsOpen)
            {
                Open();
                localcontrol = true;
            }

            try
            {
                serialPort.DiscardInBuffer();

                WriteBytes(txBytes);

                var rxBytes = ReadBytes(timeout:100);
                
                if (rxBytes==null) throw new TimeoutException("ExpectedResponse did not match. {}");
                
                bool ackd = rxBytes.SequenceEqual<byte>(txBytes);
                if (!ackd)
                {
                    throw new Exception($"Back Ack: Tx & Rx bytes did not match. " +
                        $"Tx: {BitConverter.ToString(rxBytes)} , " +
                        $"Rx: {BitConverter.ToString(txBytes)}");
                }
            }
            finally
            {
                if (localcontrol) Close();
            }
        }

    }
}
