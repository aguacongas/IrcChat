using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.JSInterop;
namespace IrcChat.Client.Services {
    public class IgnoredUsersService {
        private readonly IJSRuntime _jsRuntime;
        private IList<string> _ignoredUsers = new List<string>();

        public IgnoredUsersService(IJSRuntime jsRuntime) {
            _jsRuntime = jsRuntime;
        }

        public event Action? OnIgnoredUsersChanged;

        public async Task InitializeAsync() {
            _ignoredUsers = await GetIgnoredUsersAsync();
        }

        public async Task<IList<string>> GetIgnoredUsersAsync() {
            try {
                var result = await _jsRuntime.InvokeAsync<List<string>>("ignoredUsersManager.getIgnoredUsers");
                return result ?? new List<string>();
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error getting ignored users: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task AddIgnoredUserAsync(string userId) {
            try {
                await _jsRuntime.InvokeAsync<object>("ignoredUsersManager.addIgnoredUser", userId);
                _ignoredUsers.Add(userId);
                OnIgnoredUsersChanged?.Invoke();
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error adding ignored user: {ex.Message}");
            }
        }

        public async Task RemoveIgnoredUserAsync(string userId) {
            try {
                await _jsRuntime.InvokeAsync<object>("ignoredUsersManager.removeIgnoredUser", userId);
                _ignoredUsers.Remove(userId);
                OnIgnoredUsersChanged?.Invoke();
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error removing ignored user: {ex.Message}");
            }
        }

        public bool IsUserIgnored(string userId) {
            return _ignoredUsers.Contains(userId);
        }
    }
}