using System;
using System.Collections.Generic;
using System.Text;
using Modbus.Message;
using System.IO.Ports;
using Modbus.Util;
using System.IO;
using Modbus.IO;

namespace Modbus.IO
{
	class ModbusRtuTransport : ModbusSerialTransport
	{
		public ModbusRtuTransport()
		{
		}

		public ModbusRtuTransport(SerialPort serialPort)
			: base (serialPort)
		{
		}

		public override byte[] BuildMessageFrame(IModbusMessage message)
		{
			List<byte> messageBody = new List<byte>();
			messageBody.Add(message.SlaveAddress);
			messageBody.AddRange(message.ProtocolDataUnit);
			messageBody.AddRange(ModbusUtil.CalculateCrc(message.MessageFrame));

			return messageBody.ToArray();
		}

		public override T Read<T>(IModbusMessage request)
		{
			try
			{
				// read beginning of message frame
				byte[] frameStart = new byte[4];
				int numBytesRead = 0;
				while (numBytesRead != 4)
					numBytesRead += SerialPort.Read(frameStart, numBytesRead, 4 - numBytesRead);

				byte functionCode = frameStart[1];
				byte byteCount1 = frameStart[2];
				byte byteCount2 = frameStart[3];

				// calculate number of bytes remaining in message frame
				int bytesRemaining = NumberOfBytesToRead(functionCode, byteCount1, byteCount2);

				// read remaining bytes
				byte[] frameEnd = new byte[bytesRemaining];
				numBytesRead = 0;
				while (numBytesRead != bytesRemaining)
					numBytesRead += SerialPort.Read(frameEnd, numBytesRead, bytesRemaining - numBytesRead);

				// build complete message frame
				byte[] frame = CollectionUtil.Combine<byte>(frameStart, frameEnd);

				// check for slave exception response
				if (functionCode > Modbus.ExceptionOffset)
					throw new SlaveException(ModbusMessageFactory.CreateModbusMessage<SlaveExceptionResponse>(frame));
				
				// create message from frame
				T response = ModbusMessageFactory.CreateModbusMessage<T>(frame);

				// check crc
				if (BitConverter.ToUInt16(frame, frame.Length - 2) != BitConverter.ToUInt16(ModbusUtil.CalculateCrc(response.MessageFrame), 0))
					throw new IOException("Checksum CRC failed.");

				return response;
			}
			catch (TimeoutException te)
			{
				throw te;
			}
			catch (IOException ioe)
			{
				throw ioe;
			}
		}

		internal static int NumberOfBytesToRead(byte functionCode, byte byteCount1, byte byteCount2)
		{
			int numBytes;

			switch(functionCode)
			{
				case Modbus.ReadCoils:
				case Modbus.ReadInputs:
				case Modbus.ReadHoldingRegisters:
				case Modbus.ReadInputRegisters:
					numBytes = byteCount1 + 1;
					break;
				case Modbus.WriteSingleCoil:
				case Modbus.WriteSingleRegister:
				case Modbus.WriteMultipleCoils:
				case Modbus.WriteMultipleRegisters:
					numBytes = 4;
					break;
				default :
					numBytes = 1;
					break;
			}

			return numBytes;
		}

		public override byte[] GetMessageFrame()
		{
			throw new Exception("The method or operation is not implemented.");
		}
	}
}