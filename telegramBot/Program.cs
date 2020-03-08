using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace telegramBot
{
    class Program
    {
        static string api = "1003901903:AAEDl1DxCWC6GSk8aTrykALi_3BI9SemaBg", admin_id = "230696909", sem = "", specialisation = "", group = "";
        static int prevTableMesId = -1, prevReminderMesId = -1, remindMinutes = -1, remindHours = -1;
        static TelegramBotClient Bot = new TelegramBotClient(api);
        static List<LessonDay> days = new List<LessonDay>();
        static void Main()
        {
            BackgroundWorker bw;
            bw = new BackgroundWorker();
            bw.DoWork += BwDoWork;
            bw.RunWorkerAsync();
            Console.ReadKey();
            bw.Dispose();

            /*Uri uri = new Uri($"https://guide.herzen.spb.ru/static/schedule.php");
            string page = "";
            using (var httpClient = new System.Net.Http.HttpClient())
            {
                try { page = await httpClient.GetStringAsync(uri).ConfigureAwait(false); }
                catch (Exception ex) { Console.WriteLine($"Ошибка {ex.Message}"); return; }
            }
            var all = new Regex(@"(?<=<li.*?>)(бакалавриат|магистратура|специалитет).+?(?=</div>)").Matches(page);
            using (StreamWriter f = new StreamWriter(@"..\..\Res\Bakalavr.txt", false, System.Text.Encoding.UTF8))
            {
                for(int i = 0; i < all.Count; i++)
                {
                    f.WriteLine(all[i].Value);
                }
            }*/
        }
        static void BwDoWork(object sender, DoWorkEventArgs e)
        {

            Bot.SendTextMessageAsync(admin_id, $"Запущен бот: {Environment.UserName}");
            #region Inline
            Bot.OnInlineQuery += async (object si, Telegram.Bot.Args.InlineQueryEventArgs ei) =>
            {
                Console.WriteLine("OnInlineQuery");
            };
            #endregion
            #region Callback
            Bot.OnCallbackQuery += async (object sc, Telegram.Bot.Args.CallbackQueryEventArgs ev) =>
            {
                Message message = ev.CallbackQuery.Message;
                switch(ev.CallbackQuery.Data)
                {
                    case "Да":
                    {
                        specialisation = "isit";
                        await SendMessage(message, "Чётко, красава!😎\nВыбери курс и семестр.", BotReply(true, 4, "1 курс 1 сем", "1 курс 2 сем", "2 курс 3 сем", "2 курс 4 сем",
                                                                            "3 курс 5 сем", "3 курс 6 сем", "4 курс 7 сем", "4 курс 8 сем")).ConfigureAwait(false);

                        break;
                    }
                    case "Нет":
                    {
                        specialisation = "";
                        await SendMessage(message, "Лол, ну жди. Пока не готово.", isError: true).ConfigureAwait(false); break;
                    }
                    case "1 подгруппа":
                    case "2 подгруппа":
                    case "3 подгруппа":
                    case "4 подгруппа":
                    case "Все подгруппы":
                    {
                        group = ev.CallbackQuery.Data;
                        if (await SmthIsNull(message).ConfigureAwait(false)) return;
                        if (prevTableMesId != -1)
                        {
                            try {await Bot.DeleteMessageAsync(message.Chat.Id, prevTableMesId).ConfigureAwait(false);}
                            catch(Exception e) { Console.WriteLine(e.Message);};
                        }
                        prevTableMesId = (await SendMessage(message, $"Окей, можешь выбирать.\nПодгруппа: {group}.", BotInline(6, "Пара сейчас", "Следующая пара", "Сегодня", "Завтра", "Вся Неделя", "Напоминание")).ConfigureAwait(false)).MessageId;
                        if(prevReminderMesId != -1)
                        {
                            try { await Bot.DeleteMessageAsync(message.Chat.Id, prevReminderMesId).ConfigureAwait(false); prevReminderMesId = -1; }
                            catch (Exception e) { Console.WriteLine(e.Message); }
                            break;
                        }
                        break;
                    }
                    case "Пара сейчас":
                        await ShowLessons(message, ev.CallbackQuery.Data, 1).ConfigureAwait(false); break;
                    case "Следующая пара":
                        await ShowLessons(message, ev.CallbackQuery.Data, 2).ConfigureAwait(false); break;
                    case "Сегодня":
                        await ShowLessons(message, ev.CallbackQuery.Data).ConfigureAwait(false); break;
                    case "Завтра":
                        await ShowLessons(message, ev.CallbackQuery.Data, isTmrw: true).ConfigureAwait(false); break;
                    case "Вся Неделя":
                        await ShowLessons(message, ev.CallbackQuery.Data, isAllWeek: true).ConfigureAwait(false); break;
                    case "Напоминание" when !await SmthIsNull(message).ConfigureAwait(false):
                    {
                        if (remindMinutes != -1 || remindHours != -1)
                            remindHours = remindMinutes = -1;
                        string text = "Я тебе буду напоминать о начале первой пары следющего дня пока ты не отменишь это действие.\n" +
                                      "Сейчас нужно определиться, за сколько времени тебе отправлять напоминание.\nВыбери количетсво минут.";
                        var reply = BotInline(4, "0 мин", "5 мин", "10 мин", "15 мин", "20 мин", "25 мин", "30 мин", "35 мин", "40 мин", "45 мин", "50 мин", "55 мин");
                        if (prevReminderMesId != -1)
                        {
                            try {prevReminderMesId = (await Bot.EditMessageTextAsync(message.Chat.Id, prevReminderMesId, text, replyMarkup: reply).ConfigureAwait(false)).MessageId;}
                            catch (Exception e) { Console.WriteLine(e.Message); }
                            break;
                        }
                        prevReminderMesId = (await SendMessage(message, text, reply).ConfigureAwait(false)).MessageId;
                        break;
                    }
                    case "Отменить" when (remindMinutes != -1 && remindHours != -1) :
                    {
                        Console.WriteLine("Отменено"); break;
                    }
                }
                #region Работа с напоминанием
                var remindTime = new Regex(@"\d\d?(?=\s(час)|\s(мин))").Match(ev.CallbackQuery.Data);
                if (remindTime.Success && !await SmthIsNull(message).ConfigureAwait(false))
                {
                    if (remindTime.Groups[2].Value.Length != 0)
                    {
                        remindMinutes = Convert.ToInt32(remindTime.Groups[0].Value);
                        try
                        {
                            prevReminderMesId = (await Bot.EditMessageTextAsync(message.Chat.Id, prevReminderMesId, $"Минут: {remindMinutes}\nВыбери количетсво часов.", 
                                replyMarkup: BotInline(6, "0 часов", "1 час", "2 часа", "3 часа", "4 часа", "5 часов", "6 часов", "7 часов", "8 часов", "9 часов", "10 часов",
                                "11 часов", "12 часов", "13 часов", "14 часов", "15 часов", "16 часов", "17 часов", "18 часов", "19 часов", "20 часов", "21 час", "22 часа", "23 часа")).ConfigureAwait(false)).MessageId;
                        }
                        catch (Exception e) { Console.WriteLine(e.Message); }
                    }
                    else 
                    {
                        remindHours = Convert.ToInt32(remindTime.Groups[0].Value);
                        try 
                        {
                            prevReminderMesId = (await Bot.EditMessageTextAsync(message.Chat.Id, prevReminderMesId, $"Я буду тебе напоминать о начале первой пары каждый день за {ev.CallbackQuery.Data} {remindMinutes} минут.",
                            replyMarkup: BotInline(1, "Отменить")).ConfigureAwait(false)).MessageId;
                            await Reminder(message, remindHours, remindMinutes).ConfigureAwait(false);
                        }
                        catch (Exception e) { Console.WriteLine(e.Message); }
                    } 
                }
                #endregion
                try {await Bot.AnswerCallbackQueryAsync(ev.CallbackQuery.Id).ConfigureAwait(false);}
                catch(Exception e) {Console.WriteLine(e.Message);}
            };
            #endregion
            #region Update
            Bot.OnUpdate += async (object su, Telegram.Bot.Args.UpdateEventArgs ev) =>
            {
                Update update = ev.Update;
                Message message = update.Message;
                if (message == null || update.CallbackQuery != null || update.InlineQuery != null) return;
                if (message.Type != MessageType.Text)
                {
                    await SendMessage(message, "Ну, это не совсем текст, если что.", isError: true).ConfigureAwait(false);
                    return;
                }
                #region Обработка текста
                switch (message.Text.ToLower())
                {
                    #region Команды
                    case "/start":
                    {
                        sem = specialisation = group = "";
                        prevTableMesId = prevReminderMesId = remindMinutes = remindHours = -1;
                        await SendMessage(message, "Привет, глянь на название бота и ответь на вопрос.\n" +
                                    "Ты с ИСиТа?", BotInline(1, "Да", "Нет")).ConfigureAwait(false);
                        break;
                    }
                    case "/help": await ShowHelp(message).ConfigureAwait(false); break;
                    case "/week": await SendMessage(message, Week()).ConfigureAwait(false); break;
                    #endregion
                    case "1 курс 1 сем":
                    case "1 курс 2 сем":
                    case "2 курс 3 сем":
                    case "2 курс 4 сем":
                    case "3 курс 5 сем":
                    case "3 курс 6 сем":
                    case "4 курс 7 сем":
                    case "4 курс 8 сем":
                    {
                        await Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing).ConfigureAwait(false);
                        sem = new Regex(@"(?<=\d курс )\d(?= сем)").Match(message.Text).Value;
                        await UpdateTable(message).ConfigureAwait(false);
                        break;
                    }
                    #region Сомнительная пасхалка
                    case "спасибо":
                    case "спс":
                    case "сяп":
                    case "благодарю":
                        await SendMessage(message, "Поблагодарить программиста можно любым удобным вам способов из перечисленных:\n\n" +
                            "- Переводом на Сбербанк-Онлайн по номеру телефона 89319675184 или карты 2202201441513039\n" +
                            "- Переводом на Яндекс Деньги на счет 410014534050189\n\n" +
                            "Вот это, я понимаю, будет благодарность))").ConfigureAwait(false); break;
                    #endregion
                    default: await SendMessage(message, "", isError: true).ConfigureAwait(false); break;
                }
                #endregion
            };
            #endregion
            // прием обновлений
            Bot.StartReceiving();
        }
        #region Функционал бота
        static ReplyKeyboardMarkup BotReply(bool hide, int rows, params string[] items)
        {
            //функция создает reply клавиатуру с заданым количеством кнопок и рядов
            if (rows < 1 || rows > items.Length) throw new ArgumentOutOfRangeException(nameof(rows), "Rows more than quantity of elements or rows less than 0");
            List<List<KeyboardButton>> k_arr = new List<List<KeyboardButton>>();
            //Создаем ряд
            for (int i = 0; i < rows; i++)
                k_arr.Add(new List<KeyboardButton>());
            //Добавляем элементы
            int x = 0;
            for (int i = 0; i < items.Length; i++)
            {
                k_arr[i % rows].Add(new KeyboardButton(items[x]));
                x = (x + (items.Length / rows)) % items.Length;
                x += (i + 1) % rows == 0 ? 1 : 0;
                if (x == items.Length) x = items.Length - 1;
            }
            var keyboard = new ReplyKeyboardMarkup();
            keyboard.Keyboard = k_arr;
            keyboard.ResizeKeyboard = true;
            keyboard.OneTimeKeyboard = hide;
            return keyboard;
        }
        static InlineKeyboardMarkup BotInline(int rows, params string[] items)
        {
            //функция создает inline клавиатуру с заданым количеством кнопок и рядов
            if (rows < 1 || rows > items.Length) throw new ArgumentOutOfRangeException(nameof(rows), "Rows more than quantity of elements or rows less than 0");
            List<List<InlineKeyboardButton>> k_arr = new List<List<InlineKeyboardButton>>();
            //Создаем ряд
            for (int i = 0; i < rows; i++)
                k_arr.Add(new List<InlineKeyboardButton>());
            //Добавляем элементы
            int x = 0;
            for (int i = 0; i < items.Length; i++)
            {
                var btn = new InlineKeyboardButton();
                btn.Text = btn.CallbackData = items[x];
                k_arr[i % rows].Add(btn);
                x = (x + (items.Length / rows)) % items.Length;
                x += (i + 1) % rows == 0 ? 1 : 0;
                if (x == items.Length) x = items.Length - 1;
            }
            var keyboard = new InlineKeyboardMarkup(k_arr);
            return keyboard;
        }
        static InlineKeyboardMarkup BotInline(int rows, List<string> items)
        {
            //функция создает inline клавиатуру с заданым количеством кнопок и рядов
            if (rows < 1 || rows > items.Count) throw new ArgumentOutOfRangeException(nameof(rows), "Rows more than quantity of elements or rows less than 0");
            List<List<InlineKeyboardButton>> k_arr = new List<List<InlineKeyboardButton>>();
            //Создаем ряд
            for (int i = 0; i < rows; i++)
                k_arr.Add(new List<InlineKeyboardButton>());
            //Добавляем элементы
            for (int i = 0; i < items.Count; i++)
            {
                var btn = new InlineKeyboardButton();
                btn.Text = btn.CallbackData = items[i];
                k_arr[i % rows].Add(btn);
            }
            var keyboard = new InlineKeyboardMarkup(k_arr);
            return keyboard;
        }
        static async Task<Message> SendMessage(Message message, string text, IReplyMarkup replyMarkup = null, bool isError = false)
        {
            await Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing).ConfigureAwait(false);
            // если пришло непонятно что
            if (isError)
            {
                Random rand = new Random();
                string[] stickers = {"CAADAgADFgEAAooSqg7uSDRCqyRCrxYE", "CAADAgADpgADMNSdEUKFpVOFQXjxFgQ", "CAADAgADDQgAAhhC7giPy1Q-1PdLiBYE",
            "CAADAgADpQAD12sEFrJY5FPQA30JFgQ", "CAADAgAD5gcAApb6EgVgdqDlFwZ93xYE"};
                await Bot.SendStickerAsync(message.Chat.Id, new Telegram.Bot.Types.InputFiles.InputOnlineFile(stickers[rand.Next(0, stickers.Length)])).ConfigureAwait(false);
                string[] replyes = { "Что вы имеете в виду?", "Не понял. Повтори." };
                return await Bot.SendTextMessageAsync(message.Chat.Id, text.Length == 0 ? replyes[rand.Next(0, replyes.Length)] : text).ConfigureAwait(false);
            }
            return await Bot.SendTextMessageAsync(message.Chat.Id, text, replyMarkup: replyMarkup).ConfigureAwait(false);
        }
        static async Task<bool> SmthIsNull(Message message)
        {   
            if (sem.Length == 0 || specialisation.Length == 0)
            {
                await BackToDefault(message).ConfigureAwait(false);
                return true;
            }
            return false;
        }
        static async Task BackToDefault(Message message)
        {
            sem = specialisation = group = "";
            prevTableMesId = prevReminderMesId = remindMinutes = remindHours = -1;
            await SendMessage(message, "Так-так-так...\nЧто-то не то. Давай заново.\n" +
                "Ты с ИСиТа?", BotInline(1, "Да", "Нет")).ConfigureAwait(false);
            return;
        }
        static async Task<Lesson> ShowLessons(Message message, string action = "", int lessonNumber = -1, bool isTmrw = false, bool isAllWeek = false, int inc = -1)
        {
            if (await SmthIsNull(message).ConfigureAwait(false))
                return null;
            else if(group.Length == 0)
            {
                await BackToDefault(message).ConfigureAwait(false);
                return null;
            }
            //переменная inc нужна для функции reminder, чтобы увиличивать день до то пор, пока не найдется первый 
            DateTime now = isTmrw ? DateTime.Now.AddDays(2 + inc) : DateTime.Now;
            string result = $"Держи: {action}\n", oneDay = "", week = Week();
            bool isAllGroups = group.Equals("Все подгруппы") ? true : false, considerTime = !isAllWeek;
            int extraDays = 0;//это нужно в том случае, если юзер спрашивает неделю
            switch (now.DayOfWeek.ToString())// тк DateTime возвращает на англ языке
            {
                case "Monday": oneDay = "ПОНЕДЕЛЬНИК"; extraDays = 5; break;
                case "Tuesday": oneDay = "ВТОРНИК"; extraDays = 4; break;
                case "Wednesday": oneDay = "СРЕДА"; extraDays = 3; break;
                case "Thursday": oneDay = "ЧЕТВЕРГ"; extraDays = 2; break;
                case "Friday": oneDay = "ПЯТНИЦА"; extraDays = 1; break;
                case "Saturday": oneDay = "СУББОТА"; break;
                case "Sunday": oneDay = "ВОСКРЕСЕНЬЕ"; extraDays = 7; week = week.Equals("Верхняя") ? "Нижняя" : "Верхняя"; week += $", начиная с завтрашнего дня"; break;
            }
            result += $"Неделя: {week}\n";
            List<Lesson> day = new List<Lesson>(); int j = 0;//объявляются здесь, чтобы передать ину Reminder()
            // составляем расписание
            for (int i = 0; i<days.Count; i++)
            {
                string dayName = days[i].Name.ToUpper();
                if (considerTime && !dayName.Equals(oneDay))
                    continue;
                result += $"\n📆 {dayName}\n";
                day = days[i].Lessons;
                int number = 1;
                #warning Доделать вывод расписания
                for (j = 0; j < day.Count; j++)
                {
                    DateTime endTime = new DateTime();
                    int beginDay = day[j].BeginDate.DayOfYear, endDay = day[j].EndDate.DayOfYear;

                    if (day[j].EndTime[0] != -1)//если пара есть, то создать DateTime с точной датой
                        endTime = new DateTime(now.Year, now.Month, now.Day, day[j].EndTime[0], day[j].EndTime[1], 0); 

                    if (day[j].Name.Length != 0 //пропускаем пустые 
                     && ((isAllWeek && now.DayOfYear + extraDays >= beginDay) //пропускаем неподходящие по дате
                        || (now.DayOfYear >= beginDay && now.DayOfYear <= endDay)
                        || (beginDay == endDay && now.DayOfYear == beginDay))
                     && (week.Contains(day[j].Week) || day[j].Week.Length == 0) //пропускаем неподходящие по неделе
                     && (group.Contains(day[j].Group) || isAllGroups || day[j].Group.Equals("Общая")) //пропускаем неподходящие по группе
                     && ((considerTime  && (now.CompareTo(endTime) != 1 || isTmrw)) || isAllWeek)) //пропускаем неподходящие по времени
                    {
                        if (lessonNumber != -1 && number != lessonNumber) { number++; continue; } // если нужна одна пара
                        string grp = isAllGroups ? $"`({day[j].Group.ToString()})`" : "";
                        string corpus = day[j].Corpus.Length != 0 ? $", Корпус: {day[j].Corpus}" : "";
                        result += $"🕒 {day[j].StringBeginTime} - {day[j].StringEndTime} {grp}\n" +
                            $"*Пара: {day[j].Name} - {day[j].Type}\n*" +
                            $"_Преподаватель: {day[j].Teacher}\n" +
                            $"Аудитория: {day[j].Cabinet}{corpus}\n_";
                        if (number == lessonNumber) goto outer;
                    }
                }
            }
        outer:
            result += result.Contains("Пара:") ? "" : "У-ля-ля! Отдыхай!";
            if (inc != -1 && result.Contains("Пара")) return day[j];
            try {await Bot.EditMessageTextAsync(message.Chat.Id, message.MessageId, result, replyMarkup: BotInline(6, "Пара сейчас", "Следующая пара", "Сегодня", "Завтра", "Вся Неделя", "Напоминание"), parseMode: ParseMode.Markdown).ConfigureAwait(false);}
            catch(Exception e) {Console.WriteLine(e.Message);}
            return null;
        }
        static async Task Reminder(Message message, int hours, int minutes) 
        {
            int i = 0;
            while (true)
            {
                Lesson day = await ShowLessons(message, inc: i, lessonNumber: 1).ConfigureAwait(false);
                if (day != null) break; i++;
            }
            //var remindDay = DateTime.Now.AddDays(i+1);
            //var reg = new Regex(@"(\d?\d):(\d\d)(?=\s)").Match(result);
            //DateTime firstLes = new DateTime(remindDay.Year, remindDay.Month, remindDay.Day, Convert.ToInt32(reg.Groups[1].Value), Convert.ToInt32(reg.Groups[2].Value), 0);
            //remindDay = firstLes.AddHours(-hours).AddMinutes(-minutes);
            //if (DateTime.Now.CompareTo(remindDay) != -1)

            return;
            //await ShowLessons(message, DateTime.UtcNow.AddDays(1), ev.CallbackQuery.Data, isTmrw: true).ConfigureAwait(false);
        }
        #endregion
        #region Работа с расписанием
        static async Task UpdateTable(Message message, bool isIsit = true)
        {
            if (await SmthIsNull(message).ConfigureAwait(false)) return;
            #region Загружаем html код страницы расписания
            Uri uri;
            if (isIsit)
                uri = new Uri($"https://guide.herzen.spb.ru/static/schedule_view.php?id_group=10749&sem={sem}");
            else
                uri = new Uri($"https://guide.herzen.spb.ru/static/schedule_view.php?id_group=10749&sem={sem}");
            string page = "";
            using (var httpClient = new System.Net.Http.HttpClient())
            {
                try { page = await httpClient.GetStringAsync(uri).ConfigureAwait(false);}
                catch (Exception ex) {Console.WriteLine($"Ошибка {ex.Message}"); return; }
            }
            #endregion
            if (page.Contains("не заполнено") || page.Length == 0)
            {
                await BackToDefault(message).ConfigureAwait(false);
                return;
            }
            page = new Regex(@"\n|\t|\r").Replace(page, "");
            //ищем кол-во подгрупп
            int count = new Regex(@"(?<=th.*?)\d(?=&nbsp;подгруппа)").Matches(page).Count; 
            List<string> groups = new List<string>();
            for (int i = 0; i < count; i++)
                groups.Add($"{i + 1} подгруппа");
            groups.Add("Все подгруппы");
            if (groups.Count != 1)
                await SendMessage(message, "Тебя какая подгруппа интересует?", BotInline(count + 1, groups)).ConfigureAwait(false);
            //если подгрупп нет, то сразу продолжить работу
            else
            {
                group = "Подгрупп нет";
                await SendMessage(message, $"Окей, подгрупп у тебя нет, так что можешь приступать.",
                    BotInline(6, "Пара сейчас", "Следующая пара", "Сегодня", "Завтра", "Вся Неделя", "Напоминание")).ConfigureAwait(false);
            }
            //Подгружаем дни
            days.Clear();
            #warning Изменения в расписании считаются отдельным днем
            var s = new Regex("(?<=\"dayname\").*?(dayname|tbody)").Matches(page);
            for (int i = 0; i < s.Count-1; i++)
                days.Add(new LessonDay(s[i].ToString(), groups));
            return;
        }
        #endregion
        #region Команды
        static async Task ShowHelp(Message message)
        {
            using (StreamReader f = new StreamReader(@"..\..\Res\Commands.txt", encoding: System.Text.Encoding.UTF8))
            {
                await SendMessage(message, await f.ReadToEndAsync().ConfigureAwait(false)).ConfigureAwait(false);
            }
            return;
        }
        static string Week()
        {
            DateTime dateTime = new DateTime(2019, 9, 1);
            while (dateTime.DayOfWeek != DayOfWeek.Monday)
                dateTime = dateTime.AddDays(1);
            return DateTime.Today.Subtract(dateTime).Days/8 % 2 == 0 ? "Верхняя" : "Нижняя";
        }
        #endregion
    }
    #region Классы для работы с парами
    class LessonDay
    {
        //Класс является учебным днем. Он хранит название дня недели и массив типа Lesson, что по сути является массивом пар.
        public string Name { get; private set; }
        public List<Lesson> Lessons { get; private set; } = new List<Lesson>();
        public LessonDay(string data, List<string> groups)
        {
            Name = new Regex("понедельник|вторник|среда|четверг|пятница|суббота").Match(data).Value;
            // Массив html кодов, каждый из которых содержит всю информацию об одной паре
            var lessons = new Regex(@"(?=(\d?\d:\d\d)|<strong>|(<th>(В|Н)</th>)|<td.*?>&\w{3,6};</td>)\1?\2?.*?(?=(</td>)|<br/><strong>)").Matches(data);
            /*
             Поскольку в коде расписания не у каждой пары "рядром" с ней есть информация о ее начале, конце и неделе, в которую она проводится
             поэтому создается массив хранящий эти  значение
             Например: (*)9:45-11:30   -- указано время для последующих 3 пар
                          ТОП          -- здесь код, хранящий время(*) находится "рядом"
                          ЭВМ          -- а в этой части информация находится "далеко"
                          Риторика     (**)поэтому сохраняется значение времени и недели до тех пор, пока не встретится новое
            */
                    Match[] prevValues = new Match[3], values = new Match[3];
            Regex[] regs = { new Regex(@"(\d?\d):(\d\d)(?=\s)"), new Regex(@"(\d?\d):(\d\d)(?!\s)"), new Regex(@"(?<=<th>)(В|Н|&nbsp;)(?=</th>)") };
            string currentGroup = ""; // поскольку код, в котором указана группа пары находится "далеко", чередуем группы поочередно
            int indexGroup = 0, groupCount = groups.Count - 1;
            int x = -1;   // поскольку в случае, если пары нет и ее тег содержит атрибут rowspan="2", то ее нужно добавить еще раз
                          // тк пустая пара в коде обозначается один раз на обе недели(В/Н). Именно поэтому ее(пустую пару) необходимо продублировать через одну пару
            
            for (int i = 0; i < lessons.Count; i++)
            {   
                //(**)
                for (int j = 0; j < regs.Length; j++)
                {
                    if (regs[j].Matches(lessons[i].Value).Count != 0)
                    {
                        values[j] = regs[j].Match(lessons[i].Value);
                        prevValues[j] = values[j];
                    }
                    else values[j] = prevValues[j];
                }
                if (lessons[i].Value.Equals("<td rowspan=\"2\">&mdash;"))
                    x = i + 2;
                if (x == i) 
                {
                    currentGroup = groups[indexGroup % groupCount];
                    indexGroup++;
                    Lessons.Add(new Lesson("", "", Match.Empty, Match.Empty, Match.Empty));
                }
                //чередуем группы
                if (lessons[i].Value.Contains("colspan"))
                {
                    currentGroup = "Общая";
                    indexGroup = 0;
                }
                else if (i == 0 || lessons[i-1].Groups[4].Value.Length != 0)
                {
                    currentGroup = groups[indexGroup % groupCount];
                    indexGroup++;
                }
                //Создаём пару
                Lessons.Add(new Lesson(lessons[i].Value, currentGroup, values[0], values[1], values[2]));
            }
        }
    }
    class Lesson
    {
        //Класс является парой. Он хранит всю информацию о ней.
        public string Group { get; private set; }
        public string StringBeginTime { get; private set; }
        public string StringEndTime { get; private set; }
        public int[] BeginTime { get; private set; } = {-1,-1};
        public int[] EndTime { get; private set; } = {-1,-1};
        public string Week { get; private set; }
        public string Name { get; private set; }
        public string Type { get; private set; }
        public DateTime BeginDate { get; private set; } = DateTime.MinValue;
        public DateTime EndDate { get; private set; } = DateTime.MinValue;
        public string Teacher { get; private set; }
        public string Cabinet { get; private set; }
        public string Corpus { get; private set; }
        public Lesson(string data, string group, Match beginTime, Match endTime, Match week)
        {
            Group = group;
            StringBeginTime = beginTime.Groups[0].Value;
            StringEndTime = endTime.Groups[0].Value;
            if (beginTime != Match.Empty)
                for (int i = 0; i < 2; i++)
                {
                    BeginTime[i] = Convert.ToInt32(beginTime.Groups[i + 1].Value);
                    EndTime[i] = Convert.ToInt32(endTime.Groups[i + 1].Value);
                }
            Week = (week.Value.Equals("В") || week.Value.Equals("Н")) ? week.Value : "";
            Name = new Regex(@"(?<=<strong>).+?(?=</s)").Match(data).Value;
            Type = new Regex(@"(?<=\[)\w+(?=\])").Match(data).Value;
            var beginDate = new Regex(@"(?<=\(\s?)(\d?\d)\.(\d\d)").Match(data);
            var endDate = new Regex(@"(\d?\d)\.(\d\d)(?=\)|,|\s)").Match(data);
            if (beginDate.Value.Length != 0 && endDate.Value.Length != 0)
            {
                BeginDate = new DateTime(DateTime.Now.Year, Convert.ToInt32(beginDate.Groups[2].Value), Convert.ToInt32(beginDate.Groups[1].Value), 0, 0, 1);
                EndDate = new DateTime(DateTime.Now.Year, Convert.ToInt32(endDate.Groups[2].Value), Convert.ToInt32(endDate.Groups[1].Value), 23, 59, 59);
                if (new Regex(@"\d?\d\.\d?\d").Matches(data).Count > 2) EndDate = BeginDate;//решил не париться с датой типа: 10.10 фр. яз., 24.10,фр.яз., 7.11, фр.яз., 21.11, фр.яз.
            }
            Teacher = new Regex(@"(?<=<a.*?>).+(?=</a)").Match(data).Value;
            Cabinet = new Regex(@"(?<=ауд\.\s)\d+").Match(data).Value;
            Corpus = new Regex(@"(?<=корпус )\d+").Match(data).Value;
        }
    }
    #endregion
}