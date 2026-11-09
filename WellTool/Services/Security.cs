using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace WellTool.Services
{
    public class security
    {
        private security() { }
        private static security _instance;
        private bool _isInitialized = false;

        public string currentUser { get; private set; } = string.Empty;
        public bool isUserAuth { get; private set; } = false;
        public bool isAdmin { get; private set; } = false;
        public DataTable UserRoles { get; private set; } = new DataTable();
        


        private OracleConnector OracleConnector = new OracleConnector();
        public void Initialize()
        {
            if (!_isInitialized)
            {
                try
                {
                    // 1. Получаем пользователя Windows
                    InitializeWindowsUser();

                    // 2. Проверяем авторизацию (покажет диалог при необходимости)
                    CheckUserAuthorization();

                    // 3. Если дошли сюда - пользователь авторизован
                    CheckAdminStatus();
                    LoadUserRoles();

                    _isInitialized = true;

                    
                }
                catch (UnauthorizedAccessException)
                {
                    // Закрываем приложение, если авторизация не пройдена
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Критическая ошибка инициализации: {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                }
            }
        }
        public static security Instance
        {
            get
            {
                _instance ??= new security();
                return _instance;
            }
        }

        private void InitializeWindowsUser()
        {
            // Получаем текущего пользователя Windows
            try
            {
                
                currentUser = $"{Environment.UserDomainName}\\{Environment.UserName}".ToUpper();
            }
            catch
            {
                MessageBox.Show("Не удалось определить пользователя Windows", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                currentUser = "UNKNOWN";
            }
        }

        public bool HasAccess(string roleName)
        {
            if (!isUserAuth)
            {
                MessageBox.Show("Пользователь не авторизован",
                    "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (isAdmin) return true;

            // БЕЗОПАСНЫЙ ПОИСК: перебираем строки вручную
            foreach (DataRow row in UserRoles.Rows)
            {
                if (row["ROLES_NAME"]?.ToString() == roleName)
                    return true;
            }

            return false;
        }

        private void CheckUserAuthorization()
        {
            int maxAttempts = 3; // Максимум 3 попытки ввода
            int attempt = 0;

            while (attempt < maxAttempts)
            {
                try
                {
                    // ПАРАМЕТРИЗОВАННЫЙ ЗАПРОС для проверки пользователя
                    string checkUserSql = @"
                SELECT COUNT(*) 
                FROM HELPER_USER 
                WHERE UPPER(TRIM(USER_LOGON)) = :userLogin 
                AND ACTIVE = 1";

                    // Создаем параметр для защиты от SQL-инъекций
                    var userParam = new OracleParameter(":userLogin", OracleDbType.Varchar2)
                    {
                        Value = currentUser
                    };

                    // Выполняем запрос
                    DataTable userCheckTable = OracleConnector.GetDataTable(checkUserSql, userParam);

                    // Проверяем результат
                    if (userCheckTable.Rows.Count > 0 && userCheckTable.Rows[0][0] != DBNull.Value)
                    {
                        int userCount = Convert.ToInt32(userCheckTable.Rows[0][0]);

                        if (userCount > 0)
                        {
                            isUserAuth = true;
                            return; // Успешная авторизация
                        }
                    }

                    // Если пользователь не найден в БД
                    isUserAuth = false;

                    // Показываем системный диалог для повторного ввода
                    if (ShowWindowsCredentialsDialog(out string domain, out string username, out string password))
                    {
                        // 1. Формируем логин 
                        string testUser;

                        // Вариант 1: Если домен и имя пользователя разделены
                        if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(username))
                        {
                            testUser = $"{domain}\\{username}".ToUpper();
                        }
                        // Вариант 2: Пользователь ввел все в одном поле "ДОМЕН\Пользователь"
                        else if (!string.IsNullOrEmpty(username) && username.Contains("\\"))
                        {
                            // username уже содержит полный логин
                            testUser = username.ToUpper();
                        }
                        // Вариант 3: Только имя пользователя
                        else if (!string.IsNullOrEmpty(username))
                        {
                            testUser = $"{Environment.MachineName}\\{username}".ToUpper();
                        }
                        else
                        {
                            MessageBox.Show("Не удалось получить имя пользователя", "Ошибка",
                                            MessageBoxButton.OK, MessageBoxImage.Error);
                            attempt++;
                            continue;
                        }

                        Console.WriteLine($"Сформирован testUser для проверки: '{testUser}'");

                        // 2. ПРОВЕРЯЕМ ЭТОГО ПОЛЬЗОВАТЕЛЯ В БД ПРЯМО СЕЙЧАС
                        string immediateCheckSql = @"
                            SELECT COUNT(*)
                            FROM HELPER_USER
                            WHERE UPPER(TRIM(USER_LOGON)) = :userLogin
                            AND ACTIVE = 1";

                        var immediateParam = new OracleParameter(":userLogin", OracleDbType.Varchar2)
                        {
                            Value = testUser
                        };

                        DataTable immediateCheckTable = OracleConnector.GetDataTable(immediateCheckSql, immediateParam);

                        if (immediateCheckTable.Rows.Count > 0 &&
                            immediateCheckTable.Rows[0][0] != DBNull.Value &&
                            Convert.ToInt32(immediateCheckTable.Rows[0][0]) > 0)
                        {
                            // ПОЛЬЗОВАТЕЛЬ НАЙДЕН!
                            currentUser = testUser;
                            isUserAuth = true;
                            return; // Выходим из метода - авторизация успешна
                        }
                        else
                        {
                            // Пользователь не найден
                            MessageBox.Show($"Пользователь '{testUser}' не найден в системе или неактивен.",
                                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            currentUser = testUser;
                            attempt++;
                            // Цикл продолжится
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // Пользователь отменил авторизацию
                    MessageBox.Show(ex.Message, "Авторизация отменена",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    throw; // Пробрасываем исключение дальше
                }
                catch (OracleException oraEx)
                {
                    MessageBox.Show($"Ошибка подключения к базе данных: {oraEx.Message}",
                        "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
                    throw; // Если ошибка БД - прерываем
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    throw;
                }
            }

            // Если превышено количество попыток
            throw new UnauthorizedAccessException("Превышено количество попыток авторизации");
        }
        private void CheckAdminStatus()
        {
            // Проверяем, является ли пользователь администратором
            // Вместо хардкода нужно проверять роль из БД
            string adminUser = @"DESKTOP-ME19GMJ\LAREK";

            if (currentUser == adminUser)
            {
                isAdmin = true; // Администратор
            }
            else
            {
                isAdmin = false; // Обычный пользователь
            }

            // TODO: Сохранить userAccessLevel для использования в приложении
            // Можно использовать App.Current.Properties или статический класс
        }

        private void LoadUserRoles()
        {
            if (!isUserAuth)
            {
                UserRoles = new DataTable(); // пустая таблица
                return;
            }
            // Получаем права пользователя из таблицы HELPER_LOGON
            string rightsSql = @"
                SELECT * 
                FROM HELPER_LOGON 
                WHERE UPPER(TRIM(USER_LOGON)) = :userLogin";

            var userParam = new OracleParameter(":userLogin", OracleDbType.Varchar2)
            {
                Value = currentUser
            };

            UserRoles = OracleConnector.GetDataTable(rightsSql, userParam);
        }
        private bool ShowWindowsCredentialsDialog(out string domain, out string username, out string password)
        {
            domain = "";
            username = "";
            password = "";

            try
            {
                CREDUI_INFO credUI = new CREDUI_INFO();
                credUI.cbSize = Marshal.SizeOf(typeof(CREDUI_INFO));
                credUI.hwndParent = IntPtr.Zero; // Пробуем без родительского окна
                credUI.pszCaptionText = $"УЗ {currentUser} не обнаружена в списке пользователей.";
                credUI.pszMessageText = "Введите данные УЗ в формате \"Домен\\Логин\" или \"Логин\""; // Без сообщения
                credUI.hbmBanner = IntPtr.Zero;

                uint authPackage = 0;
                IntPtr outCredBuffer = IntPtr.Zero;
                uint outCredSize = 0;
                bool save = false;

                // ТОЛЬКО один обязательный флаг
                CREDUI_FLAGS flags = CREDUI_FLAGS.GENERIC_CREDENTIALS;

                CredUIReturnCodes result = CredUIPromptForWindowsCredentials(
                    ref credUI,
                    0,
                    ref authPackage,
                    IntPtr.Zero,
                    0,
                    out outCredBuffer,
                    out outCredSize,
                    ref save,
                    flags
                );
                Console.WriteLine($"Результат вызова: {result} (код {(int)result})");

                // 4. ОБРАБОТКА РЕЗУЛЬТАТА
                if (result == CredUIReturnCodes.NO_ERROR && outCredBuffer != IntPtr.Zero)
                {
                    int maxDomain = 256;
                    int maxUsername = 256;
                    int maxPassword = 256;

                    StringBuilder domainBuf = new StringBuilder(maxDomain);
                    StringBuilder usernameBuf = new StringBuilder(maxUsername);
                    StringBuilder passwordBuf = new StringBuilder(maxPassword);

                    bool unpackResult = CredUnPackAuthenticationBuffer(
                        0,
                        outCredBuffer,
                        outCredSize,
                        usernameBuf,
                        ref maxUsername,
                        domainBuf,
                        ref maxDomain,
                        passwordBuf,
                        ref maxPassword
                    );

                    // 6. ОБЯЗАТЕЛЬНО ОСВОБОЖДАЕМ ПАМЯТЬ
                    Marshal.FreeCoTaskMem(outCredBuffer);

                    if (unpackResult)
                    {
                        domain = domainBuf.ToString();
                        username = usernameBuf.ToString();
                        password = passwordBuf.ToString();

                        // ------- ЛОГИКА ФОРМИРОВАНИЯ ЛОГИНА -------
                        // Вариант 1: Если домен и имя пользователя разделены (правильно введены в два поля)
                        if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(username))
                        {
                            currentUser = $"{domain}\\{username}".ToUpper();
                        }
                        // Вариант 2: Пользователь ввел все в одном поле "ДОМЕН\Пользователь"
                        else if (!string.IsNullOrEmpty(username) && username.Contains("\\"))
                        {
                            // username уже содержит полный логин
                            currentUser = username.ToUpper();
                        }
                        // Вариант 3: Только имя пользователя (для локального входа)
                        else if (!string.IsNullOrEmpty(username))
                        {
                            // Используем текущий компьютер как домен
                            currentUser = $"{Environment.MachineName}\\{username}".ToUpper();
                        }
                        else
                        {
                            throw new Exception("Не удалось получить имя пользователя из диалога");
                        }
                        // -------------------------------------------------------

                        Console.WriteLine($"Распаковано: domain='{domain}', username='{username}'");
                        Console.WriteLine($"Сформирован currentUser: '{currentUser}'");

                        return true;
                    }
                }
                else if (result == CredUIReturnCodes.ERROR_CANCELLED)
                {
                    Console.WriteLine("Пользователь отменил ввод учетных данных.");
                    return false;
                }
                else
                {
                    // Логируем код ошибки для отладки
                    Console.WriteLine($"Ошибка API: {result} (код: {(int)result})");
                    // При DEBUG можно вывести MessageBox для информации
                    // MessageBox.Show($"Ошибка вызова диалога: {result}", "Ошибка");
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Исключение в ShowWindowsCredentialsDialog: {ex.Message}");
                return false;
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        // Windows API для Credential UI
        [DllImport("credui.dll", CharSet = CharSet.Unicode)]
        private static extern CredUIReturnCodes CredUIPromptForCredentials(
            ref CREDUI_INFO creditUR,
            string targetName,
            IntPtr reserved,
            int dwAuthError,
            StringBuilder userName,
            int maxUserName,
            StringBuilder password,
            int maxPassword,
            [MarshalAs(UnmanagedType.Bool)] ref bool pfSave,
            CREDUI_FLAGS flags
        );

        [DllImport("credui.dll", CharSet = CharSet.Unicode)]
        private static extern CredUIReturnCodes CredUIPromptForWindowsCredentials(
            ref CREDUI_INFO creditUR,
            int dwAuthError,
            ref uint pulAuthPackage,
            IntPtr pvInAuthBuffer,
            uint ulInAuthBufferSize,
            out IntPtr ppvOutAuthBuffer,
            out uint pulOutAuthBufferSize,
            [MarshalAs(UnmanagedType.Bool)] ref bool pfSave,
            CREDUI_FLAGS flags
        );

        [DllImport("credui.dll", CharSet = CharSet.Unicode)]
        private static extern bool CredUnPackAuthenticationBuffer(
            CREDUIWIN dwFlags,
            IntPtr pAuthBuffer,
            uint cbAuthBuffer,
            StringBuilder pszUserName,
            ref int pcchMaxUserName,
            StringBuilder pszDomainName,
            ref int pcchMaxDomainName,
            StringBuilder pszPassword,
            ref int pcchMaxPassword
        );

        // Структуры для Windows API
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDUI_INFO
        {
            public int cbSize;
            public IntPtr hwndParent;
            public string pszMessageText;
            public string pszCaptionText;
            public IntPtr hbmBanner;
        }

        private enum CredUIReturnCodes
        {
            NO_ERROR = 0,
            ERROR_CANCELLED = 1223,
            ERROR_NOT_FOUND = 1168,
            ERROR_INVALID_ACCOUNT_NAME = 1315,
            ERROR_INSUFFICIENT_BUFFER = 122,
            ERROR_INVALID_PARAMETER = 87,
            ERROR_INVALID_FLAGS = 1004,
        }

        [Flags]
        private enum CREDUI_FLAGS
        {
            GENERIC_CREDENTIALS = 0x00040000,
            SHOW_SAVE_CHECK_BOX = 0x00000040,
            ALWAYS_SHOW_UI = 0x00000080,
            DO_NOT_PERSIST = 0x00000002,
            EXPECT_CONFIRMATION = 0x00020000,
            PASSWORD_ONLY_OK = 0x00200000,
            VALIDATE_USERNAME = 0x00400000,
            COMPLETE_USERNAME = 0x00800000,
            PERSIST = 0x00001000,
            SERVER_CREDENTIAL = 0x00004000,
            EXCLUDE_CERTIFICATES = 0x00000008,
            REQUIRE_CERTIFICATE = 0x00000010,
            REQUIRE_SMARTCARD = 0x00000100,
        }

        [Flags]
        private enum CREDUIWIN
        {
            GENERIC = 0x00000001,
            CHECKBOX = 0x00000002,
            AUTHPACKAGE_ONLY = 0x00000010,
            IN_CRED_ONLY = 0x00000020,
            ENUMERATE_ADMINS = 0x00000100,
            ENUMERATE_CURRENT_USER = 0x00000200,
            SECURE_PROMPT = 0x00001000,
            PACK_32_WOW = 0x10000000,
        }

    }
}
