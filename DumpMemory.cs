using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using MsgBox = System.Windows.MessageBox;

namespace YifanLu.DumpMemory
{
    class DumpMemory : INotifyPropertyChanged
    {
        /// <summary>
        /// Simple ICommand implementation that just executes the command
        /// </summary>
        private class SimpleCommand : ICommand
        {
            /// <summary>
            /// Action to execute
            /// </summary>
            private Action<object> _action;

            /// <summary>
            /// Create a SimpleCommand
            /// </summary>
            public SimpleCommand(Action<object> action)
            {
                _action = action;
            }

            /// <summary>
            /// Not implemented
            /// </summary>
            public bool CanExecute(object parameter)
            {
                return true;
            }

#pragma warning disable 67
            /// <summary>
            /// Not used
            /// </summary>
            public event EventHandler CanExecuteChanged;
#pragma warning restore 67

            /// <summary>
            /// Execute the action
            /// </summary>
            public void Execute(object parameter)
            {
                _action.Invoke(parameter);
            }
        }

        #region Static members

        /// <summary>
        /// We need access to private field from VS to get current program debug context
        /// </summary>
        private static MethodInfo internalProgramMethod_;

        /// <summary>
        /// We need access to private field from VS to get current program stack frame
        /// </summary>
        private static MethodInfo internalStackFrameMethod_;

        /// <summary>
        /// Size of chunk to dump/write
        /// </summary>
        private const int BUFFER_SIZE = 1024;

        #endregion

        #region Events

        /// <summary>
        /// Multicast event for property change notifications.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Model

        // Description in properties below

        private string _path;
        private string _addr_expr;
        private uint _len;
        private bool _isloading;
        private bool _isdebugging;
        private bool _isbusy;
        private float _progress;

        #endregion

        #region Properties

        /// <summary>
        /// We need access to a private non-SDK exposed assembly to get debug context.
        /// </summary>
        private static Assembly InternalDebugAssembly
        {
            get
            {
                // Support VS 2010, 2011, 2012
                foreach (string str in "10;11;12".Split(new char[] { ';' }))
                {
                    try
                    {
                        return Assembly.Load(string.Format("Microsoft.VisualStudio.Debugger.Interop.Internal, Version={0}.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL", str));
                    }
                    catch (Exception)
                    {
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Action for when Browse button is clicked
        /// </summary>
        public ICommand BrowsePath
        {
            get
            {
                return new SimpleCommand((o) =>
                {
                    FileDialog file;
                    if (IsLoading)
                    {
                        file = new OpenFileDialog();
                        file.Title = Resources.InputBrowseTitle;
                    }
                    else
                    {
                        file = new SaveFileDialog();
                        file.Title = Resources.OutputBrowseTitle;
                    }
                    if (file.ShowDialog() == DialogResult.OK)
                    {
                        Path = file.FileName;
                    }
                });
            }
        }

        /// <summary>
        /// Action for when "Go" is clicked
        /// </summary>
        public ICommand Execute
        {
            get
            {
                return new SimpleCommand((o) =>
                {
                    // Save program and frame because VS does not like these to be accessed in another thread
                    IDebugProgram2 program = (internalProgramMethod_.Invoke(VsDebugger, null) as IDebugProgram2);
                    IDebugStackFrame2 frame = (internalStackFrameMethod_.Invoke(VsDebugger, null) as IDebugStackFrame2);

                    // Run in another thread so we don't freeze the whole UI when dumping/writing
                    (new System.Threading.Thread(() =>
                    {
                        if (IsLoading)
                        {
                            DoLoadMemory(program, frame);
                        }
                        else
                        {
                            DoDumpMemory(program, frame);
                        }
                    })).Start();
                });
            }
        }

        /// <summary>
        /// Action for when Load/Dump radio button is clicked
        /// </summary>
        public ICommand ResetOptions
        {
            get
            {
                return new SimpleCommand((o) =>
                {
                    Path = string.Empty;
                    Length = "0";
                });
            }
        }

        /// <summary>
        /// A file path to read from or write to
        /// </summary>
        public string Path
        {
            get
            {
                return _path;
            }

            set
            {
                if (value != _path)
                {
                    _path = value;
                    OnPropertyChanged("Path");
                }
            }
        }

        /// <summary>
        /// Address in debugee to dump from/write to. Can be any valid VS debug expression casted to void*.
        /// </summary>
        public string Address
        {
            get
            {
                return _addr_expr;
            }

            set
            {
                if (_addr_expr != value)
                {
                    _addr_expr = value;
                    OnPropertyChanged("Address");
                }
            }
        }

        /// <summary>
        /// Length to dump. Will be parsed in decimal unless it starts with "0x".
        /// </summary>
        public string Length
        {
            get
            {
                return string.Format("0x{0:X}", _len);
            }

            set
            {
                uint len;
                if (value.StartsWith("0x"))
                {
                    if (!UInt32.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out len))
                    {
                        ShowError(Resources.InvalidLengthErrorText);
                        return;
                    }
                }
                else
                {
                    if (!UInt32.TryParse(value, out len))
                    {
                        ShowError(Resources.InvalidLengthErrorText);
                        return;
                    }
                }
                if (_len != len)
                {
                    _len = len;
                    OnPropertyChanged("Length");
                }
            }
        }

        /// <summary>
        /// True if we want to load memory from file
        /// </summary>
        public bool IsLoading
        {
            get
            {
                return _isloading;
            }

            set
            {
                if (_isloading != value)
                {
                    _isloading = value;
                    OnPropertyChanged("IsLoading");
                    OnPropertyChanged("IsDumping");
                }
            }
        }

        /// <summary>
        /// True if we want to dump memory to file
        /// </summary>
        public bool IsDumping
        {
            get
            {
                return !IsLoading;
            }
        }

        /// <summary>
        /// True if control should be enabled
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                return IsDebugging && !IsBusy;
            }
        }

        /// <summary>
        /// True if we are paused in execution debug state
        /// </summary>
        public bool IsDebugging
        {
            get
            {
                return _isdebugging;
            }

            set
            {
                if (_isdebugging != value)
                {
                    _isdebugging = value;
                    OnPropertyChanged("IsEnabled");
                }
            }
        }

        /// <summary>
        /// True if we are currently running a dump or write
        /// </summary>
        public bool IsBusy
        {
            get
            {
                return _isbusy;
            }

            set
            {
                if (_isbusy != value)
                {
                    _isbusy = value;
                    OnPropertyChanged("IsEnabled");
                    OnPropertyChanged("IsBusy");
                }
            }
        }

        /// <summary>
        /// Progress of current action
        /// </summary>
        public float Progress
        {
            get
            {
                return _progress;
            }

            set
            {
                if (_progress != value)
                {
                    _progress = value;
                    OnPropertyChanged("Progress");
                }
            }
        }

        /// <summary>
        /// VS debugger context. Assumed not null.
        /// </summary>
        public IVsDebugger VsDebugger;

        #endregion

        #region Constructors

        /// <summary>
        /// Initialize the methods for accessing VS private fields
        /// </summary>
        public DumpMemory()
        {
            try
            {
                internalProgramMethod_ = InternalDebugAssembly.GetType("Microsoft.VisualStudio.Debugger.Interop.Internal.IDebuggerInternal").GetProperty("CurrentProgram").GetGetMethod();
                if ((internalProgramMethod_ == null) || !typeof(IDebugProgram2).IsAssignableFrom(internalProgramMethod_.ReturnType))
                {
                    throw new ApplicationException();
                }
                internalStackFrameMethod_ = InternalDebugAssembly.GetType("Microsoft.VisualStudio.Debugger.Interop.Internal.IDebuggerInternal").GetProperty("CurrentStackFrame").GetGetMethod();
                if ((internalStackFrameMethod_ == null) || !typeof(IDebugStackFrame2).IsAssignableFrom(internalStackFrameMethod_.ReturnType))
                {
                    throw new ApplicationException();
                }
            }
            catch (Exception)
            {
                ShowError(Resources.VSInternalMethodsNotFoundErrorText);
            }
        }

        #endregion

        #region INotifyPropertyChanged Members

        /// <summary>
        /// Checks if a property already matches a desired value.  Sets the property and
        /// notifies listeners only when necessary.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="storage">Reference to a property with both getter and setter.</param>
        /// <param name="value">Desired value for the property.</param>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers that
        /// support CallerMemberName.</param>
        /// <returns>True if the value was changed, false if the existing value matched the
        /// desired value.</returns>
        protected bool SetProperty<T>(ref T storage, T value, String propertyName = null)
        {
            if (object.Equals(storage, value)) return false;

            storage = value;
            this.OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Notifies listeners that a property value has changed.
        /// </summary>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers
        /// that support <see cref="CallerMemberNameAttribute"/>.</param>
        protected void OnPropertyChanged(string propertyName = null)
        {
            var eventHandler = this.PropertyChanged;
            if (eventHandler != null)
            {
                eventHandler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Displays an error dialog
        /// </summary>
        /// <param name="msg">Text to show</param>
        private void ShowError(string msg)
        {
            MsgBox.Show(msg, Resources.ErrorDialogTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Reads from or writes to the debug target at the evaluated expression.
        /// </summary>
        /// <param name="program">VS internal program</param>
        /// <param name="frame">VS internal frame</param>
        /// <param name="buffer">Buffer to read/write</param>
        /// <param name="length">Length to read/write</param>
        /// <param name="read">True to perform read, false to perform write</param>
        /// <param name="expr">VS debugger expression. Will be casted to void*.</param>
        /// <returns>True on success, false on failure</returns>
        private bool MemoryAction(IDebugProgram2 program, IDebugStackFrame2 frame, ref byte[] buffer, uint length, bool read, string expr)
        {
            if (!IsDebugging)
            {
                ShowError(Resources.NotInDebugContextErrorText);
                return false;
            }

            // Get the execution context (current frame) to evaluate the context
            IDebugExpressionContext2 expressionContext;
            if (frame.GetExpressionContext(out expressionContext) != VSConstants.S_OK || expressionContext == null)
            {
                ShowError(Resources.CannotGetExprContextErrorText);
                return false;
            }

            // Try to parse the expression
            IDebugExpression2 expression;
            string error;
            uint errorCharIndex;
            if (expressionContext.ParseText(string.Format("(void *)({0})", expr), enum_PARSEFLAGS.PARSE_EXPRESSION, 10, out expression, out error, out errorCharIndex) != VSConstants.S_OK || errorCharIndex > 0)
            {
                ShowError(string.Format(Resources.ParseExpressionErrorText, expr, error));
                return false;
            }

            // Try to evaluate the expression
            IDebugProperty2 debugProperty;
            if (expression.EvaluateSync(enum_EVALFLAGS.EVAL_NOSIDEEFFECTS, uint.MaxValue, null, out debugProperty) != VSConstants.S_OK || debugProperty == null)
            {
                ShowError(Resources.ExpressionEvalErrorText);
                return false;
            }

            // Get memory context of our evaluated expression
            IDebugMemoryContext2 memoryContext;
            if (debugProperty.GetMemoryContext(out memoryContext) != VSConstants.S_OK || memoryContext == null)
            {
                ShowError(Resources.CannotGetFrameErrorText);
                return false;
            }

            // Get memory accessor
            // For some reason debugProperty.GetMemoryBytes does not work so we have to do it this way
            IDebugMemoryBytes2 memoryBytes;
            if (program.GetMemoryBytes(out memoryBytes) != VSConstants.S_OK || memoryBytes == null)
            {
                ShowError(Resources.CannotGetMemoryErrorText);
                return false;
            }

            // Using the memory accessor and context, read or write to debugee memory
            if (read)
            {
                uint numread = 0;
                uint unreadable = 0;
                memoryBytes.ReadAt(memoryContext, length, buffer, out numread, ref unreadable);
                if (numread != length || unreadable > 0)
                {
                    ShowError(string.Format(Resources.ChunkUnreadableErrorText, unreadable, length));
                    return false;
                }
            }
            else
            {
                memoryBytes.WriteAt(memoryContext, length, buffer);
            }
            return true;
        }

        /// <summary>
        /// Dump memory with options from window
        /// </summary>
        /// <param name="program">VS internal program</param>
        /// <param name="frame">VS internal frame</param>
        private void DoDumpMemory(IDebugProgram2 program, IDebugStackFrame2 frame)
        {
            IsBusy = true;
            Progress = 0;
            uint left = _len;
            try
            {
                using (FileStream file = File.OpenWrite(Path))
                {
                    byte[] buffer = new byte[BUFFER_SIZE];
                    while (left > 0)
                    {
                        uint read = left < BUFFER_SIZE ? left : BUFFER_SIZE;
                        if (!MemoryAction(program, frame, ref buffer, read, true, string.Format("{0}+{1}", _addr_expr, _len - left)))
                        {
                            break;
                        }
                        file.Write(buffer, 0, (int)read);
                        left -= read;
                        Progress = (float)(_len - left) / _len * 100;
                    }
                    if (left == 0)
                    {
                        MsgBox.Show(Resources.DumpSuccessText, Resources.SuccessDialogTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError(string.Format(Resources.DumpErrorText, ex.Message, left));
            }
            IsBusy = false;
            Progress = 0;
        }

        /// <summary>
        /// Load memory with options from window
        /// </summary>
        /// <param name="program">VS internal program</param>
        /// <param name="frame">VS internal frame</param>
        private void DoLoadMemory(IDebugProgram2 program, IDebugStackFrame2 frame)
        {
            IsBusy = true;
            Progress = 0;
            uint pos = 0;
            try
            {
                using (FileStream file = File.OpenRead(Path))
                {
                    byte[] buffer = new byte[BUFFER_SIZE];
                    long len = file.Length;
                    uint read;
                    while ((read = (uint)file.Read(buffer, 0, BUFFER_SIZE)) != 0)
                    {
                        if (!MemoryAction(program, frame, ref buffer, read, false, string.Format("{0}+{1}", _addr_expr, pos)))
                        {
                            break;
                        }
                        pos += read;
                        Progress = (float)pos / len * 100;
                    }
                    if (read == 0)
                    {
                        MsgBox.Show(Resources.LoadSuccessText, Resources.SuccessDialogTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError(string.Format(Resources.LoadErrorText, ex.Message, pos));
            }
            IsBusy = false;
            Progress = 0;
        }

        #endregion
    }
}
