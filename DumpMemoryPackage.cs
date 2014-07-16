using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace YifanLu.DumpMemory
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the informations needed to show the this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    // This attribute registers a tool window exposed by this package.
    [ProvideToolWindow(typeof(DumpMemoryToolWindow))]
    [Guid(GuidList.guidDumpMemoryPkgString)]
    public sealed class DumpMemoryPackage : Package
    {
        #region Private members

        /// <summary>
        /// Environment reference for access to debugger
        /// </summary>
        private DTE _dte;

        /// <summary>
        /// Window reference for opening on menu click
        /// </summary>
        private ToolWindowPane _window;

        /// <summary>
        /// View Model for window used for setting debugger state
        /// </summary>
        private DumpMemory _dumpMemory;

        #endregion

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initilaization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            IVsDebugger debugger;

            // Get VS services
            if ((_dte = Package.GetGlobalService(typeof(DTE)) as DTE) == null)
            {
                throw new NotSupportedException(Resources.CanNotCreateWindow);
            }
            if ((debugger = Package.GetGlobalService(typeof(IVsDebugger)) as IVsDebugger) == null)
            {
                throw new NotSupportedException(Resources.DebuggerServiceNotFound);
            }

            // Listen for when we enter debug mode so we can hook to debug events
            _dte.Events.DTEEvents.ModeChanged += DTEEvents_ModeChanged;

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if ( null != mcs )
            {
                // Create the command for the menu item
                CommandID menuCommandID = new CommandID(GuidList.guidDumpMemoryCmdSet, (int)PkgCmdIDList.cmdidDumpMemory);
                MenuCommand menuItem = new MenuCommand(ShowToolWindow, menuCommandID);
                mcs.AddCommand( menuItem );
            }

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            _window = this.FindToolWindow(typeof(DumpMemoryToolWindow), 0, true);
            if ((null == _window) || (null == _window.Frame))
            {
                throw new NotSupportedException(Resources.CanNotCreateWindow);
            }

            // Find the view model for the window
            var control = _window.Content as DumpMemoryControl;
            if (control == null || (_dumpMemory = control.DataContext as DumpMemory) == null)
            {
                throw new NotSupportedException(Resources.CanNotCreateWindow);
            }

            // Pass a reference to the debugger for the view model
            _dumpMemory.VsDebugger = debugger;

            // Add event handler if we are already in debugger
            if (_dte.Debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
            {
                DTEEvents_ModeChanged(vsIDEMode.vsIDEModeDesign);
            }
        }

        #endregion

        #region Event handlers

        /// <summary>
        /// This function is called when the user clicks the menu item that shows the 
        /// tool window. See the Initialize method to see how the menu item is associated to 
        /// this function using the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void ShowToolWindow(object sender, EventArgs e)
        {
            IVsWindowFrame windowFrame = (IVsWindowFrame)_window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }

        /// <summary>
        /// Called when VS goes from design to debug mode and vice versa.
        /// </summary>
        private void DTEEvents_ModeChanged(vsIDEMode lastMode)
        {
            if (_dumpMemory != null)
            {
                _dumpMemory.IsDebugging = (lastMode != vsIDEMode.vsIDEModeDebug);
            }
            if (lastMode == vsIDEMode.vsIDEModeDebug)
            {
                _dte.Events.DebuggerEvents.OnContextChanged -= DebuggerEvents_OnContextChanged;
                _dte.Events.DebuggerEvents.OnEnterBreakMode -= DebuggerEvents_OnEnterBreakMode;
                _dte.Events.DebuggerEvents.OnEnterDesignMode -= DebuggerEvents_OnEnterRunMode;
                _dte.Events.DebuggerEvents.OnEnterRunMode -= DebuggerEvents_OnEnterRunMode;
            }
            else
            {
                _dte.Events.DebuggerEvents.OnContextChanged += DebuggerEvents_OnContextChanged;
                _dte.Events.DebuggerEvents.OnEnterBreakMode += DebuggerEvents_OnEnterBreakMode;
                _dte.Events.DebuggerEvents.OnEnterDesignMode += DebuggerEvents_OnEnterRunMode;
                _dte.Events.DebuggerEvents.OnEnterRunMode += DebuggerEvents_OnEnterRunMode;
            }
        }

        /// <summary>
        /// Called when debug context changes. Currently not needed because other events are more specific.
        /// </summary>
        private void DebuggerEvents_OnContextChanged(Process newProcess, Program newProgram, Thread newThread, StackFrame newStackFrame)
        {
            // Not needed but kept here in case it's useful one day
        }

        /// <summary>
        /// Enter debug mode. Enable dump controls.
        /// </summary>
        private void DebuggerEvents_OnEnterBreakMode(dbgEventReason reason, ref dbgExecutionAction executionAction)
        {
            if (_dumpMemory != null)
            {
                _dumpMemory.IsDebugging = true;
            }
        }

        /// <summary>
        /// Exit debug mode. Disable dump controls.
        /// </summary>
        private void DebuggerEvents_OnEnterRunMode(dbgEventReason reason)
        {
            if (_dumpMemory != null)
            {
                _dumpMemory.IsDebugging = false;
            }
        }

        #endregion
    }
}
