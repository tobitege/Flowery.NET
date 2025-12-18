using System;
using System.Windows.Input;

namespace Flowery.Services
{
    /// <summary>
    /// Simple ICommand implementation for use in controls.
    /// </summary>
    public class SimpleCommand : ICommand
    {
        private readonly Action<object?>? _executeWithParam;
        private readonly Action? _execute;
        private readonly Func<bool>? _canExecute;

        /// <summary>
        /// Creates a command with a parameterless action.
        /// </summary>
        public SimpleCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Creates a command with a parameterized action.
        /// </summary>
        public SimpleCommand(Action<object?> execute, Func<bool>? canExecute = null)
        {
            _executeWithParam = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter)
        {
            if (_executeWithParam != null)
                _executeWithParam(parameter);
            else
                _execute?.Invoke();
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
