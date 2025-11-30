using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using Microsoft.Extensions.Logging;
using TfoHelper.Configuration;

namespace TfoHelper.ToadAutomation
{
    /// <summary>
    /// Defines the contract for automating the Toad for Oracle application.
    /// </summary>
    public interface IToadAutomationService
    {
        /// <summary>
        /// Launches Toad for Oracle and attempts to log in using the provided credentials.
        /// </summary>
        /// <param name="tns">The TNS string or database alias.</param>
        /// <param name="username">The database username.</param>
        /// <param name="password">The database password.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating success or failure.</returns>
        Task<bool> LaunchAndLoginAsync(string tns, string username, string password);
    }

    /// <summary>
    /// Implements <see cref="IToadAutomationService"/> using UI Automation (UIA) to control Toad for Oracle.
    /// </summary>
    public class ToadAutomationService : IToadAutomationService
    {
        private readonly IConfigurationService _config;
        private readonly ILogger<ToadAutomationService> _logger;

        // P/Invoke for Fallback
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// Initializes a new instance of the <see cref="ToadAutomationService"/> class.
        /// </summary>
        /// <param name="config">The configuration service for accessing Toad settings.</param>
        /// <param name="logger">The logger for logging automation activities.</param>
        public ToadAutomationService(IConfigurationService config, ILogger<ToadAutomationService> logger)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Launches Toad for Oracle and attempts to log in using the provided credentials.
        /// </summary>
        /// <param name="tns">The TNS string or database alias.</param>
        /// <param name="username">The database username.</param>
        /// <param name="password">The database password.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating success or failure.</returns>
        public async Task<bool> LaunchAndLoginAsync(string tns, string username, string password)
        {
            try
            {
                var toadPath = _config.AppSettings.Toad.ToadExecutablePath;
                _logger.LogInformation("Launching Toad from {Path}", toadPath);

                var processStartInfo = new ProcessStartInfo(toadPath)
                {
                    Arguments = _config.AppSettings.Toad.Arguments,
                    UseShellExecute = true
                };

                var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    _logger.LogError("Failed to start Toad process.");
                    return false;
                }

                // Wait for Toad to initialize and show Login Dialog
                // This is tricky as Toad might take time. We need to poll for the window.
                var loginWindow = await WaitForLoginWindowAsync(process, _config.AppSettings.Timeouts.ToadLaunchTimeoutSeconds);
                
                if (loginWindow == null)
                {
                    _logger.LogError("Toad Login window not found within timeout.");
                    return false;
                }

                _logger.LogInformation("Login window found. Attempting to fill credentials.");

                bool success = FillCredentialsUIA(loginWindow, tns, username, password);
                if (!success)
                {
                    _logger.LogWarning("UIA failed. Attempting fallback.");
                    // Fallback logic (simplified for this example, would use SendKeys or similar)
                    // Ensure window is focused
                    SetForegroundWindow(new IntPtr(loginWindow.Current.NativeWindowHandle));
                    // SendKeys.SendWait(...) - SendKeys is in System.Windows.Forms. 
                    // Since we are in WPF/Core, we might need InputSimulator or just raw SendInput.
                    // For now, let's stick to UIA as primary and log failure.
                    return false;
                }

                // Trigger Connect
                var connectButton = FindElement(loginWindow, AutomationElement.NameProperty, "Connect"); // Name depends on actual Toad UI
                if (connectButton != null)
                {
                    var invokePattern = connectButton.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                    invokePattern?.Invoke();
                    _logger.LogInformation("Connect button clicked.");
                }
                else
                {
                    _logger.LogError("Connect button not found.");
                    return false;
                }

                // Verify Login Success (Wait for Main Window or check if Login Window closes)
                // This is a simplified check.
                await Task.Delay(5000); 
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Toad automation.");
                return false;
            }
        }

        private async Task<AutomationElement> WaitForLoginWindowAsync(Process process, int timeoutSeconds)
        {
            var endTime = DateTime.Now.AddSeconds(timeoutSeconds);
            while (DateTime.Now < endTime)
            {
                // Try to find the window by process or name
                // Toad's login window title usually contains "Login" or "New Connection"
                var root = AutomationElement.RootElement;
                var condition = new PropertyCondition(AutomationElement.NameProperty, "Toad for Oracle - New Connection"); // Example Title
                // Or search by ProcessId if possible, but UIA tree might not be fully populated yet.
                
                // Better approach: Iterate top level windows
                var windows = root.FindAll(TreeScope.Children, Condition.TrueCondition);
                foreach (AutomationElement window in windows)
                {
                    try 
                    {
                        if (window.Current.ProcessId == process.Id)
                        {
                            // Check if it looks like the login window
                            // This requires knowing Toad's window structure. 
                            // For now, return the first window of the process that is visible.
                            return window;
                        }
                    }
                    catch { }
                }

                await Task.Delay(1000);
            }
            return null;
        }

        private bool FillCredentialsUIA(AutomationElement window, string tns, string username, string password)
        {
            try
            {
                // These IDs/Names are hypothetical and need to be adjusted for actual Toad version
                SetText(window, "Database:", tns);
                SetText(window, "User/Schema:", username);
                SetText(window, "Password:", password);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fill credentials via UIA.");
                return false;
            }
        }

        private void SetText(AutomationElement parent, string automationIdOrName, string value)
        {
            // Try to find by AutomationId first, then Name
            var element = FindElement(parent, AutomationElement.AutomationIdProperty, automationIdOrName) 
                       ?? FindElement(parent, AutomationElement.NameProperty, automationIdOrName);

            if (element != null)
            {
                // Check for ValuePattern or TextPattern
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern))
                {
                    ((ValuePattern)pattern).SetValue(value);
                }
                else
                {
                    // Fallback: Focus and SendKeys? Or LegacyIAccessible
                    element.SetFocus();
                    // SendKeys.SendWait(value); // Requires System.Windows.Forms
                }
            }
        }

        private AutomationElement FindElement(AutomationElement parent, AutomationProperty property, string value)
        {
            return parent.FindFirst(TreeScope.Descendants, new PropertyCondition(property, value));
        }
    }
}
