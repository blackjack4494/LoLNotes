/*
copyright (C) 2011 by high828@gmail.com

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.IO;
using System.Net.Sockets;
using NotMissing.Logging;

namespace LoLNotes.Proxy
{
	public class ProxyClient
	{
		const int BufferSize = 65535;

		public TcpClient SourceTcp { get; protected set; }
		public Stream SourceStream { get; protected set; }

		public TcpClient RemoteTcp { get; protected set; }
		public Stream RemoteStream { get; protected set; }

		public IProxyHost Host { get; protected set; }

		byte[] SourceBuffer { get; set; }
		byte[] RemoteBuffer { get; set; }

		public ProxyClient(IProxyHost host, TcpClient src)
		{
			Host = host;
			SourceTcp = src;
			SourceBuffer = new byte[BufferSize];
			RemoteBuffer = new byte[BufferSize];
			RemoteTcp = new TcpClient();
		}

		protected virtual Stream GetStream(TcpClient tcp)
		{
			return tcp.GetStream();
		}

		protected virtual void ConnectRemote(string remote, int remoteport)
		{
			RemoteTcp.Connect(remote, remoteport);

			SourceStream = GetStream(SourceTcp);
			RemoteStream = GetStream(RemoteTcp);
		}

		public virtual void Start(string remote, int remoteport)
		{
			ConnectRemote(remote, remoteport);

			SourceStream.BeginRead(SourceBuffer, 0, BufferSize, OnReceive, SourceStream);
			RemoteStream.BeginRead(RemoteBuffer, 0, BufferSize, OnReceive, RemoteStream);
		}

		public virtual void Stop()
		{
			Action<Action> runandlog = delegate(Action act)
			                           	{
			                           		try
			                           		{
			                           			act();
			                           		}
			                           		catch (Exception ex)
			                           		{
			                           			StaticLogger.Warning(ex);
			                           		}
			                           	};

			runandlog(SourceTcp.Close);
			runandlog(RemoteTcp.Close);
		}

		protected virtual void OnReceive(IAsyncResult ar)
		{
			try
			{
				var stream = (Stream)ar.AsyncState;

				int read = stream.EndRead(ar);
				if (read == 0)
					throw new EndOfStreamException(string.Format("{0} socket closed", stream == SourceStream ? "Source" : "Remote"));

				if (stream == SourceStream)
				{
					Host.OnSend(this, SourceBuffer, read);
					RemoteStream.Write(SourceBuffer, 0, read);
				}
				else
				{
					Host.OnReceive(this, RemoteBuffer, read);
					SourceStream.Write(RemoteBuffer, 0, read);
				}

				stream.BeginRead(
					stream == SourceStream ? SourceBuffer : RemoteBuffer,
					0,
					BufferSize,
					OnReceive,
					stream
					);
			}
			catch (Exception ex)
			{
				Stop();
				Host.OnException(this, ex);
			}
		}
	}
}