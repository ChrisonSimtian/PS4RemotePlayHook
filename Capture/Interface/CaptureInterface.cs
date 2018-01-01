using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading;
using System.IO;

namespace Capture.Interface
{
    [Serializable]
    public delegate void MessageReceivedEvent(MessageReceivedEventArgs message);
    [Serializable]
    public delegate void DisconnectedEvent();
    [Serializable]
    public delegate void ResolutionChangeEvent(ResolutionChangeEventArgs resolutionChangeEventArgs);

    [Serializable]
    public class CaptureInterface : MarshalByRefObject
    {
        /// <summary>
        /// The client process Id
        /// </summary>
        public int ProcessId { get; set; }

        #region Events

        #region Server-side Events

        /// <summary>
        /// Server event for sending debug and error information from the client to server
        /// </summary>
        public event MessageReceivedEvent RemoteMessage;
        
        #endregion

        #region Client-side Events

        /// <summary>
        /// Client event used to notify the hook to exit
        /// </summary>
        public event DisconnectedEvent Disconnected;

        /// <summary>
        /// Client event used to notify the hook to change capture resolution
        /// </summary>
        public event ResolutionChangeEvent ResolutionChanged;
        
        #endregion

        #endregion

        /// <summary>
        /// Tell the client process to disconnect
        /// </summary>
        public void Disconnect()
        {
            SafeInvokeDisconnected();
        }

        /// <summary>
        /// Send a message to all handlers of <see cref="CaptureInterface.RemoteMessage"/>.
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void Message(MessageType messageType, string format, params object[] args)
        {
            Message(messageType, String.Format(format, args));
        }

        public void Message(MessageType messageType, string message)
        {
            SafeInvokeMessageRecevied(new MessageReceivedEventArgs(messageType, message));
        }

        public void ChangeResolution(int captureWidth, int captureHeight)
        {
            SafeInvokeChangeResolution(new ResolutionChangeEventArgs(captureWidth, captureHeight));
        }

        #region Private: Invoke message handlers

        private void SafeInvokeMessageRecevied(MessageReceivedEventArgs eventArgs)
        {
            if (RemoteMessage == null)
                return;         //No Listeners

            MessageReceivedEvent listener = null;
            Delegate[] dels = RemoteMessage.GetInvocationList();

            foreach (Delegate del in dels)
            {
                try
                {
                    listener = (MessageReceivedEvent)del;
                    listener.Invoke(eventArgs);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    RemoteMessage -= listener;
                }
            }
        }

        private void SafeInvokeChangeResolution(ResolutionChangeEventArgs resolutionChangeEventArgs)
        {
            if (ResolutionChanged == null)
                return;         //No Listeners

            ResolutionChangeEvent listener = null;
            Delegate[] dels = ResolutionChanged.GetInvocationList();

            foreach (Delegate del in dels)
            {
                try
                {
                    listener = (ResolutionChangeEvent)del;
                    listener.Invoke(resolutionChangeEventArgs);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    ResolutionChanged -= listener;
                }
            }
        }

        private void SafeInvokeDisconnected()
        {
            if (Disconnected == null)
                return;         //No Listeners

            DisconnectedEvent listener = null;
            Delegate[] dels = Disconnected.GetInvocationList();

            foreach (Delegate del in dels)
            {
                try
                {
                    listener = (DisconnectedEvent)del;
                    listener.Invoke();
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    Disconnected -= listener;
                }
            }
        }

        #endregion

        /// <summary>
        /// Used 
        /// </summary>
        public void Ping()
        {
            
        }
    }


    /// <summary>
    /// Client event proxy for marshalling event handlers
    /// </summary>
    public class ClientCaptureInterfaceEventProxy : MarshalByRefObject
    {
        #region Event Declarations

        /// <summary>
        /// Client event used to notify the hook to exit
        /// </summary>
        public event DisconnectedEvent Disconnected;

        public event ResolutionChangeEvent ResolutionChange;

        #endregion

        #region Lifetime Services

        public override object InitializeLifetimeService()
        {
            //Returning null holds the object alive
            //until it is explicitly destroyed
            return null;
        }

        #endregion


        public void DisconnectedProxyHandler()
        {
            if (Disconnected != null)
                Disconnected();
        }

        public void ResolutionChangeHandler(ResolutionChangeEventArgs resolutionChangeEventArgs)
        {
            if (ResolutionChange != null)
                ResolutionChange(resolutionChangeEventArgs);
        }
    }
}
