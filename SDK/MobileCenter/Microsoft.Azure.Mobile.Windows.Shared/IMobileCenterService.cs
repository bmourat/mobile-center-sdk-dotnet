﻿using Microsoft.Azure.Mobile.Channel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Mobile
{
    public interface IMobileCenterService
    {
        bool InstanceEnabled { get; set; }
        void OnChannelGroupReady(IChannelGroup channelGroup);
    }
}