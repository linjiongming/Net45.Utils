using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Messaging.WebPubSub
{
    public class WebPubSubMessage
    {
        public string Type { get; set; }
        public string Event { get; set; }
        public string UserId { get; set; }
        public string ConnectionId { get; set; }
        public string From { get; set; }
        public string FromUserId { get; set; }
        public string Group { get; set; }
        public string DataType { get; set; }
        public string Data { get; set; }
    }
}
