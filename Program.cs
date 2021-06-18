using System;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Newtonsoft.Json;


namespace TelegramBotWeb
{
    class Program
    {
        private static Program _program; //задание локальной переменной для данной программы
        private readonly TelegramBotClient _bot = new TelegramBotClient("Telegram Token"); //Создание клиента бота с данным токеном
        public string connectionString = "Server=(localdb)\\mssqllocaldb;Database=Users_DB;Trusted_Connection=True;MultipleActiveResultSets=true";
        static void Main(string[] args)
        {
            
            _program = new Program();
            CreateHostBuilder(args).Build().Start();
            _program.Run(args); //запуск экземпляра программы
        }
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
        private void Run(string[] args)
        {
            _bot.OnMessage += BotOnMessageReceived;
            _bot.OnCallbackQuery += BotOnCallbackQueryReceived;

            var me = _bot.GetMeAsync().Result;

            _bot.StartReceiving();
            Console.WriteLine($"Start listening for @{me.Username}\n");
            Console.ReadLine();
            _bot.StopReceiving();
        }
        private async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs e)
        {
            var message = e.CallbackQuery.Message;
            var data = e.CallbackQuery.Data;
            SqlConnection connection = new SqlConnection(connectionString);
            SqlCommand command = new SqlCommand($"SELECT CONVERT(varchar, FirstMessageTime,104) FROM Users WHERE Username_real = N'{data}'", connection);
            connection.Open();
            var time = command.ExecuteScalar().ToString();
            connection.Close();
            var first = await _bot.SendTextMessageAsync(message.Chat.Id, $"Пользователь {data} первый раз пользовался мной {time}", replyMarkup: GetKeyboard()); //Сообщение бота
            update_database(first);
        }
        private async void BotOnMessageReceived(object sender, MessageEventArgs e)
        {
            SqlConnection connection = new SqlConnection(connectionString);
            var message = e.Message;

            if (message == null || message.Type != MessageType.Text) return;
            try
            {
                Console.WriteLine($"USER MESSAGE ({message.From.Username}, Time: {message.Date.AddHours(5)})- Chat Id: {message.Chat.Id}, Name: {message.From.Username}, Message: {message.Text}");
                switch (message.Text.ToLower())
                {
                    case "/start":
                        await Task.Delay(1000);  //искусственная задержка
                        await _bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing); //показывает, что бот пишет

                        var command_for_check_users = $"IF NOT EXISTS(SELECT Users.ChatId FROM Users WHERE ChatId = N'{message.Chat.Id}')\n " +
                                                            "BEGIN\n " +
                                                                $"INSERT INTO Users(ChatId, Username_real, FirstMessageTime) VALUES({message.Chat.Id}, N'{message.From.Username}', '{message.Date.AddHours(5)}')\n " +
                                                                $"SELECT 2\n " +
                                                            "END\n " +
                                                      "ELSE\n " +
                                                            "BEGIN\n " +
                                                                $"SELECT ISNULL(Username,2) FROM Users WHERE ChatId = N'{message.Chat.Id}'\n " +
                                                            "END";
                        string text;
                        SqlCommand check_user = new SqlCommand(command_for_check_users, connection);
                        connection.Open();
                        if (check_user.ExecuteScalar().ToString() == "2") { text = "Приветствую вас!"; }
                        else { text = $"Приветствую вас, {check_user.ExecuteScalar()}!"; }
                        connection.Close();

                        var first = await _bot.SendTextMessageAsync(message.Chat.Id, text, replyMarkup: GetKeyboard()); //Сообщение бота
                        update_database(first);
                        break;

                    case "текущая температура воздуха мск":
                        await Task.Delay(1000);
                        await _bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing); //показывает, что бот пишет

                        WebRequest request = WebRequest.Create("http://api.openweathermap.org/data/2.5/weather?q=Moscow&APPID=TOKEN");
                        request.Method = "POST";
                        request.ContentType = "application/x-www-urlencoded";
                        WebResponse response = await request.GetResponseAsync();

                        string answer = string.Empty;
                        using (Stream s = response.GetResponseStream())
                        {
                            using (StreamReader reader = new StreamReader(s))
                            {
                                answer = await reader.ReadToEndAsync();
                            }
                        }
                        response.Close();

                        OpenWeather.OpenWeather oW = JsonConvert.DeserializeObject<OpenWeather.OpenWeather>(answer);
                        string cels = oW.main.temp.ToString("#");

                        var result = await _bot.SendTextMessageAsync(message.Chat.Id, $"Текущая температура воздуха в Москве — {cels} °C", replyMarkup: GetKeyboard());
                        update_database(result);
                        break;

                    case "список пользователей":
                        await Task.Delay(1000);
                        await _bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing); //показывает, что бот пишет

                        SqlCommand all_usernames_count = new SqlCommand($"SELECT COUNT(Username_real) FROM Users", connection);
                        SqlCommand all_usernames = new SqlCommand($"SELECT Username_real, FirstMessageTime FROM Users", connection);

                        connection.Open();
                        string[,] names = new string[(int)all_usernames_count.ExecuteScalar(), 2];
                        SqlDataReader reader_usernames = all_usernames.ExecuteReader();

                        int i = 0;
                        while (reader_usernames.Read())
                        {
                            names[i, 0] = reader_usernames[0].ToString();
                            names[i, 1] = reader_usernames[1].ToString();
                            i++;
                        }
                        reader_usernames.Close();
                        connection.Close();

                        var keyboardMarkup = new InlineKeyboardMarkup(GetInlineKeyboard(names));

                        var inline_names = await _bot.SendTextMessageAsync(message.Chat.Id, "Список всех пользователей: ", replyMarkup: keyboardMarkup);
                        update_database(inline_names);
                        break;

                    case "ввести имя":
                        await Task.Delay(1000);
                        await _bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing); //показывает, что бот пишет
                        SqlCommand check_username = new SqlCommand($"SELECT ISNULL(Username,1) FROM Users WHERE ChatId = {message.Chat.Id}", connection);

                        connection.Open();
                        if (check_username.ExecuteScalar().ToString() != "1")
                        {
                            var name_choosed = await _bot.SendTextMessageAsync(message.Chat.Id, $"Вы уже задали себе имя, {check_username.ExecuteScalar()}", replyMarkup: GetKeyboard());
                            update_database(name_choosed);
                        }
                        else
                        {
                            var name_request = await _bot.SendTextMessageAsync(message.Chat.Id, "Пожалуйста, введите желаемое имя пользователя", replyMarkup: new ForceReplyMarkup());
                            update_database(name_request);
                        }
                        connection.Close();
                        break;

                    default:
                        await Task.Delay(1000);
                        await _bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing); //показывает, что бот пишет

                        SqlCommand com = new SqlCommand($"SELECT LastBotMessage FROM Users WHERE ChatId = {message.Chat.Id}", connection);
                        connection.Open();
                        if (com.ExecuteScalar().ToString() == "Пожалуйста, введите желаемое имя пользователя") //Если была введена команда изменения имени
                        {
                            Telegram.Bot.Types.Message exists;
                            SqlCommand update_name = new SqlCommand($"IF EXISTS(SELECT Username FROM Users WHERE Username = N'{message.Text}') BEGIN SELECT 1; END\n " +
                                                                    $"ELSE BEGIN UPDATE Users SET Username = N'{message.Text}' WHERE ChatId = {message.Chat.Id}; SELECT 2; END", connection);
                            if (update_name.ExecuteScalar().ToString() == "2")
                            {
                                exists = await _bot.SendTextMessageAsync(message.Chat.Id, $"Теперь я буду называть вас так \"{message.Text}\".", replyMarkup: GetKeyboard());
                            }
                            else
                            {
                                exists = await _bot.SendTextMessageAsync(message.Chat.Id, $"Данное имя уже занято, напишите другое.", replyMarkup: GetKeyboard());
                            }
                            update_database(exists);
                            connection.Close();
                        }
                        else
                        {
                            connection.Close();
                            var def = await _bot.SendTextMessageAsync(message.Chat.Id, $"Я не понимаю что обозначает \"{message.Text}\"\n😢", replyMarkup: GetKeyboard());
                            update_database(def);
                        }
                        break;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        private static IReplyMarkup GetKeyboard()
        {
            return new ReplyKeyboardMarkup()
            {
                Keyboard = new[]
                {
                    new KeyboardButton[] { "Ввести имя" },
                    new KeyboardButton[] { "Текущая температура воздуха МСК", "Список пользователей" }
                },
                ResizeKeyboard = true
            };
        }
        private void update_database(Telegram.Bot.Types.Message message) //Обновление базы данных последним сообщением бота
        {
            Console.WriteLine($"BOT REPLY ({message.From.Username}, Time: {message.Date.AddHours(5)}) - Chat Id: {message.Chat.Id}, Message: {message.Text}");
            SqlConnection connection = new SqlConnection(connectionString);
            SqlCommand update_data = new SqlCommand($"UPDATE Users SET LastBotMessage = N'{message.Text}' WHERE ChatId = N'{message.Chat.Id}'", connection);
            connection.Open();
            update_data.ExecuteNonQuery();
            connection.Close();
        }
        private static InlineKeyboardButton[][] GetInlineKeyboard(string[,] stringArray)
        {
            var keyboardInline = new InlineKeyboardButton[stringArray.Length / 2][]; //ставим количество строк равным количеству элементов в массиве
            var keyboardButtons = new InlineKeyboardButton[stringArray.Length / 2]; //создаём массив кнопок размером с количеством элементов массива
            for (var i = 0; i < stringArray.Length / 2; i++)
            {
                keyboardButtons[i] = new InlineKeyboardButton
                {
                    Text = stringArray[i, 0],
                    CallbackData = stringArray[i, 0] //в качестве ответа передаётся имя пользователя
                };
            }
            for (var j = 1; j <= stringArray.Length / 2; j++)
            {
                keyboardInline[j - 1] = keyboardButtons.Take(1).ToArray();
                keyboardButtons = keyboardButtons.Skip(1).ToArray();
            }

            return keyboardInline;
        }
    }
}


