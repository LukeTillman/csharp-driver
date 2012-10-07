﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace Cassandra.Native
{
    public class CassandraEventArgs : EventArgs
    {
        public CassandraEventType CassandraEventType;
        public IPEndPoint IPEndPoint;
        public string Message;
    }

    public delegate void CassandraEventHandler(object sender, CassandraEventArgs e);

    [Flags]
    public enum CassandraEventType { TopologyChange = 0x01, StatusChange = 0x02 }

    public partial class CassandraConnection : IDisposable
    {

        public event CassandraEventHandler CassandraEvent;

        private void EventOccured(ResponseFrame frame)
        {
            var response = FrameParser.Parse(frame);
            if(response is EventResponse)
            {
                if(CassandraEvent!=null)
                    CassandraEvent.Invoke(this, (response as EventResponse).CassandraEventArgs);
                return;
            }
            throw new InvalidOperationException();
        }

        public IAsyncResult BeginRegisterForCassandraEvent(CassandraEventType eventTypes, AsyncCallback callback, object state)
        {
            var socketStream = CreateSocketStream();

            Internal.AsyncResult<IOutput> ar = new Internal.AsyncResult<IOutput>(callback, state, this, "REGISTER");

            BeginJob(ar, new Action<int>((streamId) =>
            {
                Evaluate(new RegisterForEventRequest(streamId, eventTypes), ar, streamId, new Action<ResponseFrame>((frame2) =>
                {
                    var response = FrameParser.Parse(frame2);
                    if (response is ReadyResponse)
                        JobFinished(ar, streamId, new OutputVoid());
                    else
                        ProtocolErrorHandlerAction(new ErrorActionParam() { AsyncResult = ar, Response = response, streamId = streamId });

                }), socketStream);
            }), socketStream);

            return ar;
        }

        public IOutput EndRegisterForCassandraEvent(IAsyncResult result)
        {
            return Internal.AsyncResult<IOutput>.End(result, this, "REGISTER");
        }

        public IOutput RegisterForCassandraEvent(CassandraEventType eventTypes)
        {
            var r = BeginRegisterForCassandraEvent(eventTypes, null, null);
            r.AsyncWaitHandle.WaitOne();
            return EndRegisterForCassandraEvent(r);
        }
    }
}