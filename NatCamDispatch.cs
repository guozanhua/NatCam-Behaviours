/* 
*   NatCam
*   Copyright (c) 2016 Yusuf Olokoba
*/

//Incompatible with Windows Store Application (WSA) platform because of absence of System.Threading

using UnityEngine;
using System;
using System.Collections;
using System.Threading;
using NatCamU.Extensions;
using Ext = NatCamU.Extensions.NatCamExtensions;
using Queue = System.Collections.Generic.List<System.Action>;

namespace NatCamU {
    
    namespace Internals {
        
        public sealed class NatCamDispatch {

            public bool isRunning {get {return running;}}
            
            private DispatchMode mode;
            private Thread targetThread, mainThread, workerThread;
            private Queue invocation, update, execution;
            private MonoBehaviour timesliceProvider;
            private Coroutine routine;
            private readonly object queueLock = new object();
            private volatile bool running;
            

            #region --Ctor & Dtor--

            public static NatCamDispatch Prepare (DispatchMode Mode, MonoBehaviour TimesliceProvider = null, int Rate = 15) {
                NatCamDispatch dispatch = new NatCamDispatch {
                    mode = Mode,
                    mainThread = Thread.CurrentThread,
                    timesliceProvider = TimesliceProvider,
                    invocation = new Queue(),
                    update = new Queue(),
                    execution = new Queue(),
                    running = true
                };
                dispatch.workerThread = Mode == DispatchMode.Asynchronous ? new Thread(() => dispatch.Routine<Camera>(dispatch.Update, Rate)) : null;
                dispatch.targetThread = Mode == DispatchMode.Asynchronous ? dispatch.workerThread : dispatch.mainThread;
                if (Mode == DispatchMode.Synchronous) {
                    if (dispatch.timesliceProvider) dispatch.routine = Routine<Camera>(dispatch.Update, new WaitForEndOfFrame()).Invoke(dispatch.timesliceProvider);
                    else Camera.onPostRender += dispatch.Update;
                }
                else dispatch.workerThread.Start();
                Ext.Log("NatCam: Initialized "+Mode+" Dispatcher");
                return dispatch;
            }

            public static void Release (NatCamDispatch dispatch) {
                if (dispatch == null || !dispatch.running) return;
                dispatch.running = false;
                if (dispatch.mode == DispatchMode.Synchronous) {
                    if (dispatch.routine != null) dispatch.routine.Terminate(dispatch.timesliceProvider);
                    else Camera.onPostRender -= dispatch.Update;
                }
                else dispatch.workerThread.Join();
                Ext.Log("NatCam: Released "+dispatch.mode+" Dispatcher");
            }

            private NatCamDispatch () {}

            ~NatCamDispatch () {
                invocation.Clear(); update.Clear(); execution.Clear();
                invocation =
                update =
                execution = null;
                mainThread =
                workerThread =
                targetThread = null;
                timesliceProvider = null;
                routine = null;
            }
            #endregion


            #region --Dispatching--

            public void Dispatch (Action action) {
                //Check that we aren't already on the target thread
                if (Thread.CurrentThread.ManagedThreadId == targetThread.ManagedThreadId) action();
                //Enqueue
                else lock (queueLock) invocation.Add(action);
            }

            public void DispatchContinuous (Action action) {
                //Enqueue
                lock (queueLock) update.Add(action);
            }

            private void Update (Camera unused) {
                //Lock
                lock (queueLock) {
                    execution.AddRange(invocation);
                    execution.AddRange(update);
                    invocation.Clear();
                }
                //Execute
                execution.ForEach(e => e());
                execution.Clear();
            }
            #endregion
            

            #region --Utility--

            private static IEnumerator Routine<T> (Action<T> action, YieldInstruction yielder) where T : class {
                while (true) {
                    yield return yielder;
                    action(null);
                }
            }

            private void Routine<T> (Action<T> action, int rate) where T : class { //INCOMPLETE //Use AutoResetEvent.WaitOne instead of Thread.Sleep
                while (running) {
                    Thread.Sleep(1000/rate);
                    action(null);
                }
            }
            #endregion
        }

        public enum DispatchMode : byte {
            Synchronous = 1,
            Asynchronous = 2
        }
    }
}