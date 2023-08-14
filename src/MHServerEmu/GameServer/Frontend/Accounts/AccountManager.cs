﻿using System.Text.Json;
using Gazillion;
using MHServerEmu.Common;
using MHServerEmu.Networking;
using static MHServerEmu.Networking.AuthServer;

namespace MHServerEmu.GameServer.Frontend.Accounts
{
    public static class AccountManager
    {
        private const int MinimumPasswordLength = 3;

        private static readonly Logger Logger = LogManager.CreateLogger();
        private static readonly string SavedDataDirectory = $"{Directory.GetCurrentDirectory()}\\SavedData";

        private static List<Account> _accountList = new();
        private static Dictionary<string, Account> _emailAccountDict = new();

        public static bool IsInitialized { get; private set; }

        static AccountManager()
        {
            LoadAccounts();
            IsInitialized = true;
        }

        public static Account GetAccountByEmail(string email, string password, out ErrorCode? errorCode)
        {
            errorCode = null;

            if (_emailAccountDict.ContainsKey(email) == false)
            {
                errorCode = ErrorCode.IncorrectUsernameOrPassword1;
                return null;
            }

            Account account = _emailAccountDict[email];

            if (Cryptography.VerifyPassword(password, account.PasswordHash, account.Salt))
            {
                if (account.IsBanned)
                {
                    errorCode = ErrorCode.AccountBanned;
                    return null;
                }
                else if (account.IsArchived)
                {
                    errorCode = ErrorCode.AccountArchived;
                    return null;
                }
                else if (account.IsPasswordExpired)
                {
                    errorCode = ErrorCode.PasswordExpired;
                    return null;
                }
                else
                {
                    return account;
                }
            }
            else
            {
                errorCode = ErrorCode.IncorrectUsernameOrPassword1;
                return null;
            }
        }

        public static Account GetAccountByLoginDataPB(LoginDataPB loginDataPB, out ErrorCode? errorCode)
        {
            string email = loginDataPB.EmailAddress.ToLower();
            return GetAccountByEmail(email, loginDataPB.Password, out errorCode);
        }

        public static void CreateAccount(string email, string password)
        {
            if (_emailAccountDict.ContainsKey(email) == false)
            {
                if (password.Length >= MinimumPasswordLength)
                {
                    Account account = new((ulong)_accountList.Count + 1, email, password);
                    _accountList.Add(account);
                    _emailAccountDict.Add(email, account);
                    SaveAccounts();

                    Logger.Info($"Created a new account {email}");
                }
                else
                {
                    Logger.Warn($"Failed to create account: password must be at least {MinimumPasswordLength} characters long");
                }
            }
            else
            {
                Logger.Warn($"Failed to create account: email {email} is already used by another account");
            }
        }

        public static string BanAccount(string email)
        {
            if (_emailAccountDict.ContainsKey(email))
            {
                if (_emailAccountDict[email].IsBanned == false)
                {
                    _emailAccountDict[email].IsBanned = true;
                    return $"Successfully banned account {email}";
                }
                else
                {
                    return $"Account {email} is already banned";
                }
            }
            else
            {
                return $"Cannot ban {email}: account not found";
            }
        }

        public static string UnbanAccount(string email)
        {
            if (_emailAccountDict.ContainsKey(email))
            {
                if (_emailAccountDict[email].IsBanned)
                {
                    _emailAccountDict[email].IsBanned = false;
                    return $"Successfully unbanned account {email}";
                }
                else
                {
                    return $"Account {email} is not banned";
                }
            }
            else
            {
                return $"Cannot unban {email}: account not found";
            }
        }

        private static void LoadAccounts()
        {
            if (_accountList.Count == 0)
            {
                if (Directory.Exists(SavedDataDirectory) == false) Directory.CreateDirectory(SavedDataDirectory);

                string path = $"{SavedDataDirectory}\\Accounts.json";
                if (File.Exists(path))
                {
                    _accountList = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(path));

                    foreach (Account account in _accountList)
                        _emailAccountDict.Add(account.Email, account);

                    Logger.Info($"Loaded {_accountList.Count} accounts");
                }
            }
            else
            {
                Logger.Warn("Failed to load accounts: accounts are already loaded!");
            }
        }

        private static void SaveAccounts()
        {
            if (Directory.Exists(SavedDataDirectory) == false) Directory.CreateDirectory(SavedDataDirectory);

            string path = $"{SavedDataDirectory}\\Accounts.json";
            File.WriteAllText(path, JsonSerializer.Serialize(_accountList));
        }
    }
}