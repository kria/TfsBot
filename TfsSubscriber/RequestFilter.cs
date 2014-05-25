using Microsoft.TeamFoundation.Framework.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevCore.TfsBot.TfsSubscriber
{
    class RequestFilter : ITeamFoundationRequestFilter
    {
        public void BeginRequest(TeamFoundationRequestContext requestContext)
        {

        }

        public void EndRequest(TeamFoundationRequestContext requestContext)
        {
        }

        public void EnterMethod(TeamFoundationRequestContext requestContext)
        {
            //Log(String.Format("EnterMethod: {0} ({1})", requestContext.Method.Name, requestContext.Method.MethodType));
            //Log("Status: " + requestContext.Status.GetType());
            //foreach (var key in requestContext.Method.Parameters.AllKeys)
            //{
            //    Log(String.Format("{0} : {1}", key, requestContext.Method.Parameters[key]));
            //}
            //foreach (var key in requestContext.Items.Keys)
            //{
            //    Log(String.Format("{0} :: {1}", key, requestContext.Items[key].GetType()));
            //}


        }

        public void LeaveMethod(TeamFoundationRequestContext requestContext)
        {
            //Log(String.Format("LeaveMethod: {0} ({1})", requestContext.Method.Name, requestContext.Method.MethodType));
            //Log("Status: " + requestContext.Status.GetType());
            //foreach (var key in requestContext.Method.Parameters.AllKeys)
            //{
            //    Log(String.Format("{0} : {1}", key, requestContext.Method.Parameters[key]));
            //}
            //foreach (var key in requestContext.Items.Keys)
            //{
            //    Log(String.Format("{0} :: {1}", key, requestContext.Items[key].GetType()));
            //}
        }

        public void RequestReady(TeamFoundationRequestContext requestContext)
        {
        }
    }
}
