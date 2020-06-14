﻿using BlubLib.DotNetty.Codecs;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using System;

namespace ProudNet.Codecs
{
    internal class ProudFrameDecoder : LengthFieldBasedFrameDecoder
    {
        public ProudFrameDecoder(int maxFrameLength)
            : base(maxFrameLength, 2, 1)
        { }

        protected override long GetUnadjustedFrameLength(IByteBuffer buffer, int offset, int length, ByteOrder order)
        {
            buffer = buffer.WithOrder(ByteOrder.LittleEndian);
            var scalarPrefix = buffer.GetByte(offset++);

            // lengthFieldOffset from constructor + scalarPrefix from above
            var bytesLeft = buffer.ReadableBytes - Math.Abs(buffer.ReaderIndex - offset) + 1;
            switch (scalarPrefix)
            {
                case 1:
                    return bytesLeft < 1 ? 1 : buffer.GetByte(offset) + 1;

                case 2:
                    return bytesLeft < 2 ? 2 : buffer.GetShort(offset) + 2;

                case 4:
                    return bytesLeft < 4 ? 4 : buffer.GetInt(offset) + 4;

                default:
                    throw new ProudException("Invalid scalar prefix " + scalarPrefix);
            }
        }

        protected override IByteBuffer ExtractFrame(IChannelHandlerContext context, IByteBuffer buffer, int index, int length)
        {
            buffer = buffer.WithOrder(ByteOrder.LittleEndian);
            byte[] data = buffer.ToArray();
            CMessage msg = new CMessage(new CByteArray(data));
            short magic = 0;
            if (!msg.Read(ref magic)
                && magic != 0x5713)
            {
                throw new ProudException("Bad format in Magic");
            }

            CByteArray coreMessage = new CByteArray();
            if (!msg.Read(ref coreMessage))
            {
                throw new ProudException("Bad format in Frame");
            }

            IByteBuffer coreMsgBuffer = Unpooled.Buffer(coreMessage._buffer.Length);
            coreMsgBuffer.WriteBytes(coreMessage._buffer);
            return coreMsgBuffer;
        }
    }
}
