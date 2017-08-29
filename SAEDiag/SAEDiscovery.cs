using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using J2534;

namespace SAE
{
    public class SAEDiscovery
    {
        public static List<ISAESession> ConnectEverything(List<J2534Device> Devices)
        {
            //List<ISAESession> Sessions = new List<ISAESession>();
            //foreach(J2534Device Device in Devices)
            //{
            //    SAEChannelFactory SAEChannels = new SAEChannelFactory(Device);
            //    Channel SAEChannel;
            //    while ((SAEChannel = SAEChannels.NextChannel()) != null)
            //    {
            //        J1979Session Session = new J1979Session(SAEChannel, true);
            //        if (Session.Broadcast())
            //            Sessions.Add(Session);
            //        else
            //            SAEChannel.Disconnect();
            //    }
            //}
            //return Sessions;
            throw new NotImplementedException();
        }
    }
}
