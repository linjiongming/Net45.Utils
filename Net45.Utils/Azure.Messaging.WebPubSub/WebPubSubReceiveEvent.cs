using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Messaging.WebPubSub
{
    public class WebPubSubReceiveEventArgs : EventArgs
    {
        public WebPubSubReceiveEventArgs(WebPubSubMessage message, CancellationToken cancellation = default(CancellationToken))
        {
            Message = message;
            Cancellation = cancellation;
        }
        public WebPubSubMessage Message { get; }
        public CancellationToken Cancellation { get; }
    }

    public delegate Task WebPubSubReceiveEventHandler(WebPubSubServiceClient client, WebPubSubReceiveEventArgs e);
}
