using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using NLog;

namespace State_Machine_FWR
{
    [Serializable]
    //сообщение по TCP
    public struct TCP_message
    {
        public ConcurrentDictionary<int, float> State_info;
        public string State_name;
        public bool Ready_for_lock;
        public int MaxMin_version;
    }
    public struct TCP_client_message
    {
        public ConcurrentDictionary<int, float> New_state_info;
        public string Target_state_name;
    }
    [Serializable]
    public struct Comand_COM
    {
        public int id;
        //public int module_Type;
        public bool is_complex_parse;
        public bool is_ini; //выполняется только 1 раз при запуске цикла
        public bool is_write; //да - значит команда
        public string name;
        public string read_data_COM;
        public string write_data_COM;
        //public string End_line;
        public string prefix; //адрес + название команды
        public string postfix;//постфикс
        public float current_data; //значение сейчас
        public float target_step_plus; //проверить тип числа
        public float target_step_minus;
        public bool check_sum1;
        public bool check_sum2;
        public int delay;
        //public bool is_ascii;
    }
    [Serializable]
    public struct Thyr_data
    {
        public float pressure; //возможно нужно double

    }
    [Serializable]
    public struct UPS_data
    {
        public float i_p_voltage;
        public float i_p_f_voltage;
        public float o_p_voltage;
        public int o_p_current;
        public float i_p_frequency;
        public int Bat_level;
        public float temperature;
        public int ups_fault;
    }
    [Serializable]
    public struct M7066_data
    {
        public string conf;
        public int ch0;
        public int ch1;
        public int ch2;
        public int ch3;
        public int ch4;
        public int ch5;
        public int ch6;

    }
    [Serializable]
    public struct I7000_data
    {
        public string conf;
        public float ch0;
        public float ch1;
        public float ch2;
        public float ch3;
    }
    [Serializable]
    public struct State_slice
    {
        public UPS_data Ups_data;
        public Thyr_data Thyr_data;
        public I7000_data I7024_data;
        public M7066_data M7066_Data;
    }

    [Serializable]
    public struct MAX_MIN_data_for_modules
    {
        public ConcurrentDictionary<int, float> MAX_data_slice;
        public ConcurrentDictionary<int, float> TAR_data_slice;
        public ConcurrentDictionary<int, float> MIN_data_slice;
        public ConcurrentDictionary<int, float> Step_plus;
        public ConcurrentDictionary<int, float> Step_minus;
    }
    [Serializable]
    public struct Com_port
    {
        public string Name { get; set; }
        public int Baudrate { get; set; }
        public int Parity { get; set; }
        public int Databits { get; set; }
        public int Stopbits { get; set; }
        public bool Is_handshake { get; set; }
        public int Readtimeout { get; set; }
        public int Writetimeout { get; set; }
        public string EndLine { get; set; }

    }
    [Serializable]
    public struct COMs_settings
    {
        public Com_port COM_port_1;
        public Com_port COM_port_2;
        public Com_port COM_port_3;
        public Com_port COM_port_4;
        public Com_port COM_port_5;
        public Com_port COM_port_6;
        public Com_port COM_port_7;
        public Com_port COM_port_8;
    }
    [Serializable]
    public struct Timers_settings
    {
        public Timer_set COM1_timer;
        public Timer_set COM2_timer;
        public Timer_set COM3_timer;
        public Timer_set COM4_timer;
        public Timer_set COM5_timer;
        public Timer_set COM6_timer;
        public Timer_set COM7_timer;
        public Timer_set COM8_timer;
    }
    [Serializable]
    public struct Timer_set
    {
        public int period;
        public int start_delay;
    }
    [Serializable]
    public struct Data_for_COM_thread
    {
        public SerialPort port;
        public List<Comand_COM> list;
    }
    [Serializable]
    public struct State_data
    {
        public int MAX_MIN_version;
        public MAX_MIN_data_for_modules max_min_target;
        public COMs_settings com_settings;
        public Timers_settings timers_set;
        public List<Comand_COM> list1;
        public List<Comand_COM> list2;
        public List<Comand_COM> list3;
        public List<Comand_COM> list4;
        public List<Comand_COM> list5;
        public List<Comand_COM> list6;
        public List<Comand_COM> list7;
        public List<Comand_COM> list8;
    }
    [Serializable]
    public struct SM_data
    {
        public State_data st_SS;
        public State_data st_PHLL;
        public State_data st_PLLL;
        public State_data st_Er;
        public State_data st_Em;
        public State_data st_ALL;
        public State_data st_AN;
        public State_data st_HVSB;
        //public ConcurrentDictionary<int, Comand_COM> Сom_dict;
        public List<string> IP_adress_port;
        //public int TCP1_port;
    }
    [Serializable]
    //выходные данный после опроса COM
    public struct COM_data_Out
    {
        public float ans;
        public bool need_break;
        public bool need_error_procces;
        public string message;
    }


    //машина состояний
    public class State_Machine
    {
        public State _state = null; //изменен модификатор c private to public 
        public SM_data _Data;
        public delegate void Log_Context_Handler(string mess);
        public event Log_Context_Handler Log_m;
        public static bool is_listening = true;
        public bool is_ready_manage = true;
        public List<IPEndPoint> Locked_TCPclient = new List<IPEndPoint>();
        public List<IPEndPoint> TCPclients = new List<IPEndPoint>();
        //private string Server_ans;
        //public TcpListener tcpListemer_master;
        public CancellationTokenSource s_cts = new CancellationTokenSource();
        public CancellationToken token;
        public static Logger Logg = LogManager.GetCurrentClassLogger();
        public string settings_path = "";
        public static bool is_TCP_running = true;
        //Info - только для данных с установки
        //Debug - все события

        public State_Machine(State state, Log_Context_Handler logger, string path)
        {
            Log_m = logger;
            Logg.Debug("start State Machine");
            _Data = new SM_data();
            settings_path = path;
            //десриализуем параметры
            State_data SS_state_data = De_serialize(path, "\\State_SS_Settings.json");
            State_data PLLL_state_data = De_serialize(path, "\\State_PLLL_Settings.json");
            State_data PHLL_state_data = De_serialize(path, "\\State_PHLL_Settings.json");
            State_data Er_state_data = De_serialize(path, "\\State_Er_Settings.json");
            State_data Em_state_data = De_serialize(path, "\\State_Em_Settings.json");
            State_data ALL_state_data = De_serialize(path, "\\State_ALL_Settings.json");
            State_data AN_state_data = De_serialize(path, "\\State_AN_Settings.json");
            State_data HVSB_state_data = De_serialize(path, "\\State_HVSB_Settings.json");
            SM_data SM_all_data = new SM_data
            {
                st_Em = Em_state_data,
                st_Er = Er_state_data,
                st_SS = SS_state_data,
                st_PHLL = PHLL_state_data,
                st_PLLL = PLLL_state_data,
                st_ALL = ALL_state_data,
                st_AN = AN_state_data,
                st_HVSB = HVSB_state_data
            };
            _Data = SM_all_data;
            Logg.Debug("Settings were loaded");
            //cts = new CancellationTokenSource();

            //зпускаем сервак            
            //TCP_Server_async.Log_TCP_Server_Handler lll = new TCP_Server_async.Log_TCP_Server_Handler(logger);
            //TCP_Server_async serv = new TCP_Server_async(lll);
            //serv.Run_Listener(_Data.TCP1_port);
            //TCP_Client cll = new TCP_Client();
            Logg.Trace("start TCP server");

            StartTCPserver();
            //ConnectAs_clientTCP("self test");
            Logg.Debug("start first state");
            TransitionTo(state);
        }
        //деструктор
        ~State_Machine()
        {
            //cts.Cancel();
            //cts.Dispose();
            Logg.Debug("Stop State Machine");
            LogManager.Flush();
        }
        /// <summary>
        /// вывод словаря из дефолтных настроек
        /// </summary>
        /// <param name="name"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public ConcurrentDictionary<int, float> Got_target_list_default(string name, string path)
        {
            State_data temp = new State_data();
            switch (name)
            {
                case "SS":
                    temp = De_serialize(path, "\\State_SS_Settings.json");
                    break;
                case "PLLL":
                    temp = De_serialize(path, "\\State_PLLL_Settings.json");
                    break;
                case "Er":
                    temp = De_serialize(path, "\\State_Er_Settings.json");
                    break;
                case "Em":
                    temp = De_serialize(path, "\\State_Em_Settings.json");
                    break;
                case "PHLL":
                    temp = De_serialize(path, "\\State_PHLL_Settings.json");
                    break;
                case "ALL":
                    temp = De_serialize(path, "\\State_ALL_Settings.json");
                    break;
                case "AN":
                    temp = De_serialize(path, "\\State_AN_Settings.json");
                    break;
                case "HVSB":
                    temp = De_serialize(path, "\\State_HVSB_Settings.json");
                    break;
            }
            return temp.max_min_target.TAR_data_slice;
        }
        //десериалиатор настроек
        private State_data De_serialize(string path, string name)
        {
            State_data data = new State_data();
            using (StreamReader file = File.OpenText(path + name))
            {
                JsonSerializer ser = new JsonSerializer
                {
                    Formatting = Newtonsoft.Json.Formatting.Indented
                };
                data = (State_data)ser.Deserialize(file, typeof(State_data));
            }
            return data;
        }
        // Контекст позволяет изменять объект Состояния во время выполнения.
        public void TransitionTo(State state)
        {
            //обновляем все таргеты на те, что стоят в файлах
            _state = state;
            _state.SetContext(this);

        }

        // Контекст делегирует часть своего поведения текущему объекту
        // Состояния.
        public void SS_set()
        {
            _state.SS_handle();
        }
        public void PHLL_set()
        {
            _state.PHLL_handle();
        }
        public void PLLL_set()
        {
            _state.PLLL_handle();
        }
        public void Er_set()
        {
            _state.Er_handle();
        }
        public void Em_set()
        {
            _state.Em_handle();
        }
        public void ALL_set()
        {
            _state.ALL_handle();
        }
        public void AN_set()
        {
            _state.AN_handle();
        }
        public void HVSB_set()
        {
            _state.HVSB_handle();
        }
        //ручное разлочивание
        public void Unock_TCP_client()
        {
            //object locker
            Logg.Trace("Unlock TCP_client_list");
            if (Locked_TCPclient != null)
            {
                Locked_TCPclient.Clear();
                is_ready_manage = true;
            }
        }
        //runTimeoutListening
        private async Task<TcpClient> Listen_withTimwout(TcpListener listener, CancellationToken token)
        {
            //CancellationTokenSource s_cts = new CancellationTokenSource();
            TcpClient tcpClient = null;
            //Log_m?.Invoke("begin listen with timeout");
            try
            {
                //token.CancelAfter(500);
                //Log_m?.Invoke("before await client");
                tcpClient = await listener.AcceptTcpClientAsync();
                //tcpClient = listener.AcceptTcpClient();
            }
            catch (TaskCanceledException)
            {
                Log_m?.Invoke("timeout TCP listener");
                Logg.Trace("timeout TCP listener");
            }
            return tcpClient;
        }
        private TCP_message TCP_message_construct()
        {
            TCP_message mess = new TCP_message();
            //'state';'name of state';'можно ли залочиться для управления';'версия МАКС-МИН'
            //конструктор сообщения            
            if (Locked_TCPclient != null && Locked_TCPclient.Count > 0)
            {
                //is_ready_manage = false;
                mess.Ready_for_lock = false;
            }
            else
            {
                //is_ready_manage = true;
                mess.Ready_for_lock = true;
            }
            //Logg.Trace("Count of locked clients = " + Locked_TCPclient.Count.ToString());
            //if (Locked_TCPclient != null && Locked_TCPclient.Count > 0) Logg.Trace("Locked client = " + IPAddress.Parse(((IPEndPoint)Locked_TCPclient[0]).Address.ToString()));
            mess.MaxMin_version = _state.MAXMIN_version;
            mess.State_info = _state.StateInfo;
            mess.State_name = _state.name;
            //state_inf = state_inf + ";" + _state.name +";"+ is_ready_manage.ToString()+";"+_state.MAXMIN_version ;
            return mess;
        }
        //простое ответ-запрос общение с клинетом
        private async Task<bool> Simple_TCP_talk(NetworkStream TCPstream, CancellationToken ttt, string IP_cl)
        {
            bool ans = false;
            //if (TCPstream.)
            if (_state != null && _state.StateInfo != null)
            {
                //Log_m(TCPstream.CanTimeout.ToString()); //всегда true
                //Log_m(TCPstream.ReadTimeout.ToString()); //по умолчанию -1
                //Log_m(TCPstream.WriteTimeout.ToString()); //по умолчанию -1                
                //нужна проверка на доступность соединения
                try
                {
                    //ждем запроса клиента
                    var buffer_in = new byte[1024];
                    var byte_count = await TCPstream.ReadAsync(buffer_in, 0, buffer_in.Length, ttt);
                    string client_q = Encoding.ASCII.GetString(buffer_in, 0, byte_count);
                    Logg.Trace("Client "+ IP_cl + "= " + client_q);
                    if (client_q != "")
                    {
                        string m = "default string";
                        try
                        {
                            m = JsonConvert.SerializeObject(TCP_message_construct());
                        }
                        catch (Exception ex)
                        {
                            Logg.Trace("exeption in serialization\n" + ex.ToString());
                        }
                        byte[] message = Encoding.ASCII.GetBytes(m);
                        //пишем ему ответ
                        Logg.Trace("Server = " + m);
                        await TCPstream.WriteAsync(message, 0, message.Length, ttt);
                        buffer_in = new byte[1024];
                        var b_c = await TCPstream.ReadAsync(buffer_in, 0, buffer_in.Length, ttt);
                        string client_ask = Encoding.ASCII.GetString(buffer_in, 0, b_c);
                        //парсим команды полученые                        
                        //Log_m?.Invoke("Client = " + client_ask);
                        Logg.Trace("Client "+ IP_cl + " = " + client_ask);
                        Client_ans_Parse(client_ask);
                        ans = true;
                    }
                    else
                        ans = false;
                }
                catch (IOException ex)
                {
                    Logg.Trace("error in simple message" + ex.ToString());
                    ans = false;
                    //вернуть результат
                }
                catch (Exception ex)
                {
                    Logg.Trace("error in simple message" + ex.ToString());
                    ans = false;
                }

            }
            return ans;
        }

        /// <summary>
        /// обработка обещния с полученным клиентом
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        private async Task Process_TCP_Client(TcpClient c, string IP_cl)
        {

            //цикл общения с таймаутом
            bool is_asking = true;
            //using (var TCPstream = c.GetStream())
            //{
            NetworkStream stream = c.GetStream();
            while (is_asking)
            {
                //Logg.Trace("tick in while awaiting 1");
                CancellationTokenSource s_cst_client = new CancellationTokenSource();
                try
                {
                    s_cst_client.CancelAfter(TimeSpan.FromSeconds(5));
                    bool res = await Simple_TCP_talk(stream, s_cst_client.Token, IP_cl);
                    Logg.Trace("after simple talk, res = " + res.ToString());
                    if (!res) is_asking = false;
                }
                catch (OperationCanceledException)
                {
                    Logg.Trace("time out for client = " + IP_cl);
                    //Unock_TCP_client();
                    is_asking = false;
                }
                finally
                {
                    s_cst_client.Dispose();
                }
            }
            Logg.Trace("end using this connection");
            stream.Close();
            //}

        }
        //парсим ответ клиента
        //list[0] - connect/disconnect
        //list[1] - новое состояние
        //list[2] - список команд
        private void Client_ans_Parse(string commands)
        {
            try
            {
                //если это норм комманды
                if (commands != null && commands != "no")
                {
                    //парсим
                    TCP_client_message mess = JsonConvert.DeserializeObject<TCP_client_message>(commands);
                    //пушим
                    if (mess.Target_state_name != "")
                    {
                        _state.Push_change_state(mess.Target_state_name);
                    }
                    if (mess.New_state_info.Count > 0)
                    {
                        foreach (KeyValuePair<int, float> val in mess.New_state_info)
                        {
                            _state.Push_target_value(val.Key, val.Value);
                        }
                    }
                }
                else
                {
                    //фиговые команды получили
                    Logg.Debug("bad commands, or no to do");
                    Logg.Trace("bad commands, or no to do");
                }
            }
            catch (Exception ex)
            {
                //Log_m?.Invoke("Error in parsing TCP client answer "+ ex.Message + ex.ToString());
                Logg.Debug("Error in parsing TCP client answer " + ex.ToString());
                Logg.Trace("Error in parsing TCP client answer " + ex.ToString());
            }
        }
        /// <summary>
        /// старт TCP сервера
        /// </summary>
        private async void StartTCPserver()
        {
            var tcpListerner = TcpListener.Create(7451);
            tcpListerner.Start();
            Logg.Trace("server started");
            while (is_TCP_running) // тут какое-то разумное условие выхода
            {
                try
                {
                    s_cts = new CancellationTokenSource();
                    s_cts.CancelAfter(5000);
                    token = s_cts.Token;
                    var res = await Listen_withTimwout(tcpListerner, token);
                    if (res != null && res.Connected)
                    {
                        //клиент реален
                        //Log_m?.Invoke("Connected client: "+true_client.Client.RemoteEndPoint.ToString());
                        //Log_m?.Invoke("Update remote client: " + IPAddress.Parse(((IPEndPoint)true_client.Client.RemoteEndPoint).Address.ToString()) + ":" + ((IPEndPoint)true_client.Client.RemoteEndPoint).Port.ToString());
                        Logg.Trace("Got client: " + IPAddress.Parse(((IPEndPoint)res.Client.RemoteEndPoint).Address.ToString()) + ":" + ((IPEndPoint)res.Client.RemoteEndPoint).Port.ToString());
                        //Log_m?.Invoke("Connected client: " + IPAddress.Parse(((IPEndPoint)true_client.Client.LocalEndPoint).Address.ToString()) + "on port number " + ((IPEndPoint)true_client.Client.LocalEndPoint).Port.ToString());
                        //EndPoint LocAddr = true_client.Client.LocalEndPoint;
                        Process_TCP_Client(res, IPAddress.Parse(((IPEndPoint)res.Client.RemoteEndPoint).Address.ToString()).ToString());
                    }
                }
                catch (OperationCanceledException ex)
                {
                    Logg.Trace("waiting and processing client timeout" + ex.Message);
                    Unock_TCP_client();
                }
                catch (Exception ex)
                {
                    Logg.Trace("Error in TCP client awaiting \n" + ex.ToString());
                }
                finally
                {

                    s_cts.Dispose();
                }
                Logg.Trace("waiting next client .....");
            }
            tcpListerner.Stop();
            Logg.Trace("Listener stopped");
        }
    }
    public abstract class State
    {
        protected State_Machine _context;
        public string name;
        public int MAXMIN_version;
        //public System.Threading.Timer TCP_timer1;
        public System.Threading.Timer COM1_timer;
        public System.Threading.Timer COM2_timer;
        public System.Threading.Timer COM3_timer;
        public System.Threading.Timer COM4_timer;
        public System.Threading.Timer COM5_timer;
        public System.Threading.Timer COM6_timer;
        public System.Threading.Timer COM7_timer;
        public System.Threading.Timer COM8_timer;
        //public System.Threading.Timer TCP_timer;
        public SerialPort _serialPort_8;
        public SerialPort _serialPort_7;
        public SerialPort _serialPort_6;
        public SerialPort _serialPort_5;
        public SerialPort _serialPort_4;
        public SerialPort _serialPort_3;
        public SerialPort _serialPort_2;
        public SerialPort _serialPort_1;
        public MAX_MIN_data_for_modules max_min_tar_data;
        public delegate void Log_Handler(string mess);
        public event Log_Handler Log_message;
        public bool need_kill_all = false;
        public AutoResetEvent event_1 = new AutoResetEvent(true);
        public AutoResetEvent event_2 = new AutoResetEvent(true);
        public AutoResetEvent event_3 = new AutoResetEvent(true);
        public AutoResetEvent event_4 = new AutoResetEvent(true);
        public AutoResetEvent event_5 = new AutoResetEvent(true);
        public AutoResetEvent event_6 = new AutoResetEvent(true);
        public AutoResetEvent event_7 = new AutoResetEvent(true);
        public AutoResetEvent event_8 = new AutoResetEvent(true);
        //public AutoResetEvent event_TCP = new AutoResetEvent(true);
        public ConcurrentDictionary<int, float> StateInfo = new ConcurrentDictionary<int, float>(); //писывает состояние стэйта
        private ConcurrentDictionary<int, float> Push_target_list = new ConcurrentDictionary<int, float>(); //список изменений в целевые значения
        //public List<string> IP_adress;
        private bool first_loop1 = true;
        private bool first_loop2 = true;
        private bool first_loop3 = true;
        private bool first_loop4 = true;
        private bool first_loop5 = true;
        private bool first_loop6 = true;
        private bool first_loop7 = true;
        private bool first_loop8 = true;
        private bool[] error_count = new bool[8] { false, false, false, false, false, false, false, false }; //false - не было ошибок
        private bool[] finished_previous = new bool[8] { true, true, true, true, true, true, true, true }; //true - если предыдущее закончено
        private int[] ini_comands_count = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 }; //количество комманд инициации
        public Logger Log_ = LogManager.GetCurrentClassLogger();
        //просто пихает в список изменение, старые перезаписываются
        public void Push_target_value(int key_target, float value_target)
        {
            if (max_min_tar_data.MAX_data_slice.ContainsKey(key_target) && (value_target > max_min_tar_data.MAX_data_slice[key_target]))
            {
                value_target = max_min_tar_data.MAX_data_slice[key_target];
            }
            if (max_min_tar_data.MIN_data_slice.ContainsKey(key_target) && (value_target < max_min_tar_data.MIN_data_slice[key_target]))
            {
                value_target = max_min_tar_data.MIN_data_slice[key_target];
            }
            if (Push_target_list.ContainsKey(key_target))
            {
                Push_target_list[key_target] = value_target;
            }
            else
            {
                Push_target_list.TryAdd(key_target, value_target);
            }
        }
        //команды изменения состояния
        public void Push_change_state(string name)
        {
            switch (name)
            {
                case "SS":
                    SS_handle();
                    break;
                case "PHLL":
                    PHLL_handle();
                    break;
                case "Er":
                    Er_handle();
                    break;
                case "PLLL":
                    PLLL_handle();
                    break;
                case "Em":
                    Em_handle();
                    break;
                case "HVSB":
                    HVSB_handle();
                    break;
                case "AN":
                    AN_handle();
                    break;
                case "ALL":
                    ALL_handle();
                    break;
                default:
                    Log_message?.Invoke("we go defoault");
                    Log_.Debug("some problems with state recongnition");
                    //_context.Logg.Debug
                    break;
            }
        }
        //запуск таймеров на все порты
        public void RunTimer(List<Comand_COM> list1,
                              List<Comand_COM> list2,
                              List<Comand_COM> list3,
                              List<Comand_COM> list4,
                              List<Comand_COM> list5,
                              List<Comand_COM> list6,
                              List<Comand_COM> list7,
                              List<Comand_COM> list8,
                              Timers_settings settings,
                              COMs_settings com_settings, MAX_MIN_data_for_modules m_t_m)
        {
            max_min_tar_data = m_t_m;
            if (list1 != null && settings.COM1_timer.period != 0)
            {
                TimerCallback ticker_com1 = new TimerCallback(Tick_com1);
                _serialPort_1 = Ini_comport(com_settings.COM_port_1);
                Data_for_COM_thread com1_data = new Data_for_COM_thread
                {
                    port = _serialPort_1,
                    list = list1
                };
                try
                {
                    _serialPort_1.Open();
                    COM1_timer = new Timer(ticker_com1, com1_data, settings.COM1_timer.start_delay, settings.COM1_timer.period);
                }
                catch
                {
                    Log_message?.Invoke(name + " no COMPORT" + com_settings.COM_port_1.Name);
                    Log_.Debug(name + " no COMPORT" + com_settings.COM_port_1.Name);
                    //_context.
                    //_context.
                }
            }
            if (list2 != null && settings.COM2_timer.period != 0)
            {
                TimerCallback ticker_com2 = new TimerCallback(Tick_com2);
                _serialPort_2 = Ini_comport(com_settings.COM_port_2);
                Data_for_COM_thread com2_data = new Data_for_COM_thread
                {
                    port = _serialPort_2,
                    list = list2
                };
                try
                {
                    _serialPort_2.Open();
                    COM2_timer = new Timer(ticker_com2, com2_data, settings.COM2_timer.start_delay, settings.COM2_timer.period);
                }
                catch
                {
                    Log_message?.Invoke(name + " no COMPORT" + com_settings.COM_port_2.Name);
                    Log_.Debug(name + " no COMPORT" + com_settings.COM_port_2.Name);
                }
            }
            if (list3 != null && settings.COM3_timer.period != 0)
            {
                TimerCallback ticker_com3 = new TimerCallback(Tick_com3);
                _serialPort_3 = Ini_comport(com_settings.COM_port_3);
                Data_for_COM_thread com3_data = new Data_for_COM_thread
                {
                    port = _serialPort_3,
                    list = list3
                };
                try
                {
                    _serialPort_3.Open();
                    COM3_timer = new Timer(ticker_com3, com3_data, settings.COM3_timer.start_delay, settings.COM3_timer.period);
                }
                catch
                {
                    Log_message?.Invoke(name + " no COMPORT" + com_settings.COM_port_3.Name);
                    Log_.Debug(name + " no COMPORT" + com_settings.COM_port_3.Name);
                }
            }
            if (list4 != null && settings.COM4_timer.period != 0)
            {
                TimerCallback ticker_com4 = new TimerCallback(Tick_com4);
                _serialPort_4 = Ini_comport(com_settings.COM_port_4);
                Data_for_COM_thread com4_data = new Data_for_COM_thread
                {
                    port = _serialPort_4,
                    list = list4
                };
                try
                {
                    _serialPort_3.Open();
                    COM4_timer = new Timer(ticker_com4, com4_data, settings.COM4_timer.start_delay, settings.COM4_timer.period);
                }
                catch
                {
                    Log_message?.Invoke(name + " no COMPORT" + com_settings.COM_port_4.Name);
                    Log_.Debug(name + " no COMPORT" + com_settings.COM_port_4.Name);
                }
            }
            if (list5 != null && settings.COM5_timer.period != 0)
            {
                TimerCallback ticker_com5 = new TimerCallback(Tick_com5);
                _serialPort_5 = Ini_comport(com_settings.COM_port_5);
                Data_for_COM_thread com5_data = new Data_for_COM_thread
                {
                    port = _serialPort_5,
                    list = list5
                };
                try
                {
                    _serialPort_5.Open();
                    COM5_timer = new Timer(ticker_com5, com5_data, settings.COM5_timer.start_delay, settings.COM5_timer.period);
                }
                catch
                {
                    Log_message?.Invoke(name + " no COMPORT" + com_settings.COM_port_5.Name);
                    Log_.Debug(name + " no COMPORT" + com_settings.COM_port_5.Name);
                }
            }
            if (list6 != null && settings.COM6_timer.period != 0)
            {
                TimerCallback ticker_com6 = new TimerCallback(Tick_com6);
                _serialPort_6 = Ini_comport(com_settings.COM_port_6);
                Data_for_COM_thread com6_data = new Data_for_COM_thread
                {
                    port = _serialPort_6,
                    list = list6
                };
                try
                {
                    _serialPort_6.Open();
                    COM6_timer = new Timer(ticker_com6, com6_data, settings.COM6_timer.start_delay, settings.COM6_timer.period);
                }
                catch
                {
                    Log_message?.Invoke(name + " no COMPORT" + com_settings.COM_port_6.Name);
                    Log_.Debug(name + " no COMPORT" + com_settings.COM_port_6.Name);
                }
            }
            if (list7 != null && settings.COM7_timer.period != 0)
            {
                TimerCallback ticker_com7 = new TimerCallback(Tick_com7);
                _serialPort_7 = Ini_comport(com_settings.COM_port_7);
                Data_for_COM_thread com7_data = new Data_for_COM_thread
                {
                    port = _serialPort_7,
                    list = list7
                };
                try
                {
                    _serialPort_7.Open();
                    COM7_timer = new Timer(ticker_com7, com7_data, settings.COM7_timer.start_delay, settings.COM7_timer.period);
                }
                catch
                {
                    Log_message?.Invoke(name + " no COMPORT" + com_settings.COM_port_7.Name);
                    Log_.Debug(name + " no COMPORT" + com_settings.COM_port_7.Name);
                }
            }
            if (list8 != null && settings.COM8_timer.period != 0)
            {
                TimerCallback ticker_com8 = new TimerCallback(Tick_com8);
                _serialPort_8 = Ini_comport(com_settings.COM_port_8);
                Data_for_COM_thread com8_data = new Data_for_COM_thread
                {
                    port = _serialPort_8,
                    list = list8
                };
                try
                {
                    _serialPort_8.Open();
                    COM8_timer = new Timer(ticker_com8, com8_data, settings.COM8_timer.start_delay, settings.COM8_timer.period);
                }
                catch
                {
                    Log_message?.Invoke(name + " no COMPORT" + com_settings.COM_port_8.Name);
                    Log_.Debug(name + " no COMPORT" + com_settings.COM_port_8.Name);
                }
            }

        }
        //тик для таймера 1
        private void Tick_com1(object state)
        {
            //проверка нужно ли останавливать таймер
            //берем из обджект список комманд
            try
            {
                if (!need_kill_all)
                {
                    Data_for_COM_thread data = (Data_for_COM_thread)state;
                    COM_ask(data.port, data.list, first_loop1, 1);
                    if (first_loop1) first_loop1 = false;
                }
                else
                {
                    //шлем событие, что можно убивать
                    event_1.Set();
                }
            }
            catch (Exception ex)
            {
                Log_message?.Invoke("exeption timer 1 " + ex.Message.ToString() + "\n" + ex.ToString());
                Log_.Error("exeption timer 1 " + ex.Message.ToString() + "\n" + ex.ToString());
            }
            finally
            {
                //Log_message?.Invoke("block finally");
            }
        }
        //тик для таймера 2
        private void Tick_com2(object state)
        {
            //проверка нужно ли останавливать таймер
            //берем из обджект список комманд
            try
            {
                if (!need_kill_all)
                {
                    Data_for_COM_thread data = (Data_for_COM_thread)state;
                    COM_ask(data.port, data.list, first_loop2, 2);
                    if (first_loop2) first_loop2 = false;
                }
                else
                {
                    //убиваем
                    event_2.Set();
                }
            }
            catch (Exception ex)
            {
                Log_message?.Invoke("exeption timer 2 " + ex.Message.ToString() + "\n" + ex.ToString());
                Log_.Error("exeption timer 2 " + ex.Message.ToString() + "\n" + ex.ToString());
            }
            finally
            {
                //Log_message?.Invoke("block finally");
            }
        }
        //тик для таймера 3
        private void Tick_com3(object state)
        {
            //проверка нужно ли останавливать таймер
            //берем из обджект список комманд
            try
            {
                if (!need_kill_all)
                {
                    Data_for_COM_thread data = (Data_for_COM_thread)state;
                    COM_ask(data.port, data.list, first_loop3, 3);
                    if (first_loop3) first_loop3 = false;
                }
                else
                {
                    //убиваем
                    event_3.Set();
                }
            }
            catch (Exception ex)
            {
                Log_message?.Invoke("exeption timer 3" + ex.Message.ToString() + "\n" + ex.ToString());
                Log_.Error("exeption timer 3 " + ex.Message.ToString() + "\n" + ex.ToString());
            }
            finally
            {
                //Log_message?.Invoke("block finally");
            }
        }
        //тик для таймера 4
        private void Tick_com4(object state)
        {
            //проверка нужно ли останавливать таймер
            //берем из обджект список комманд
            try
            {
                if (!need_kill_all)
                {
                    Data_for_COM_thread data = (Data_for_COM_thread)state;
                    COM_ask(data.port, data.list, first_loop4, 4);
                    if (first_loop4) first_loop4 = false;
                }
                else
                {
                    //убиваем
                    event_4.Set();
                }
            }
            catch (Exception ex)
            {
                Log_message?.Invoke("exeption timer 4 " + ex.Message.ToString() + "\n" + ex.ToString());
                Log_.Error("exeption timer 4 " + ex.Message.ToString() + "\n" + ex.ToString());
            }
            finally
            {
                //Log_message?.Invoke("block finally");
            }
        }
        public string Str_get_from_dict(ConcurrentDictionary<int, float> t)
        {
            string ans = "";
            foreach (var n in t)
            {
                ans += n.Key.ToString() + " " + n.Value.ToString() + ";";
            }
            return ans;
        }
        //тик для таймера 5
        private void Tick_com5(object state)
        {
            //проверка нужно ли останавливать таймер
            //берем из обджект список комманд
            try
            {
                if (!need_kill_all)
                {
                    //Log_message?.Invoke("tick timer 5");
                    Data_for_COM_thread data = (Data_for_COM_thread)state;
                    COM_ask(data.port, data.list, first_loop5, 5);
                    if (first_loop5) first_loop5 = false;
                    //string str = Str_get_from_dict();
                    //Log_.Trace(Str_get_from_dict(Push_target_list));
                }
                else
                {
                    //убиваем
                    event_5.Set();
                }
            }
            catch (Exception ex)
            {
                Log_message?.Invoke("exeption timer 5 " + ex.Message.ToString() + "\n" + ex.ToString());
                Log_.Error("exeption timer 5 " + ex.Message.ToString() + "\n" + ex.ToString());
            }
            finally
            {
                //Log_message?.Invoke("block finally");
            }
        }
        //тик для таймера 6
        private void Tick_com6(object state)
        {
            //проверка нужно ли останавливать таймер
            //берем из обджект список комманд
            try
            {
                if (!need_kill_all)
                {
                    Data_for_COM_thread data = (Data_for_COM_thread)state;
                    COM_ask(data.port, data.list, first_loop6, 6);
                    if (first_loop6) first_loop6 = false;
                }
                else
                {
                    //убиваем
                    event_6.Set();
                }
            }
            catch (Exception ex)
            {
                Log_message?.Invoke("exeption timer 6 " + ex.Message.ToString() + "\n" + ex.ToString());
                Log_.Error("exeption timer 6 " + ex.Message.ToString() + "\n" + ex.ToString());
            }
            finally
            {
                //Log_message?.Invoke("block finally");
            }
        }
        //тик для таймера 7
        private void Tick_com7(object state)
        {
            //проверка нужно ли останавливать таймер
            //берем из обджект список комманд
            try
            {
                if (!need_kill_all)
                {
                    Data_for_COM_thread data = (Data_for_COM_thread)state;
                    COM_ask(data.port, data.list, first_loop7, 7);
                    if (first_loop7) first_loop7 = false;
                }
                else
                {
                    //убиваем
                    event_7.Set();
                }
            }
            catch (Exception ex)
            {
                Log_message?.Invoke("exeption timer 7 " + ex.Message.ToString() + "\n" + ex.ToString());
                Log_.Error("exeption timer 7 " + ex.Message.ToString() + "\n" + ex.ToString());
            }
            finally
            {
                //Log_message?.Invoke("block finally");
            }
        }
        //тик для таймера 8
        private void Tick_com8(object state)
        {
            //проверка нужно ли останавливать таймер
            //берем из обджект список комманд
            try
            {
                if (!need_kill_all)
                {
                    Data_for_COM_thread data = (Data_for_COM_thread)state;
                    COM_ask(data.port, data.list, first_loop8, 8);
                    if (first_loop8) first_loop8 = false;
                }
                else
                {
                    //убиваем
                    event_8.Set();
                }
            }
            catch (Exception ex)
            {
                Log_message?.Invoke("exeption timer 8 " + ex.Message.ToString() + "\n" + ex.ToString());
                Log_.Error("exeption timer 8 " + ex.Message.ToString() + "\n" + ex.ToString());
            }
            finally
            {
                //Log_message?.Invoke("block finally");
            }
        }
        //настройка СОМ порта
        private SerialPort Ini_comport(Com_port portsett)
        {
            SerialPort port = new SerialPort(portsett.Name, portsett.Baudrate);
            //ЗАГЛУШКА с определение четности и стоп битов
            if (portsett.Parity == 0)
            {
                port.Parity = Parity.None;
            }
            if (portsett.Parity == 1)
            {
                port.Parity = Parity.Odd;
            }
            if (portsett.Stopbits == 1)
            {
                port.StopBits = StopBits.One;
            }
            port.DataBits = portsett.Databits;
            port.ReadTimeout = portsett.Readtimeout;
            port.WriteTimeout = portsett.Writetimeout;
            //if (COMs_setting.COM_port_1.is)
            if (portsett.EndLine != null) port.NewLine = portsett.EndLine;
            return port;
        }
        //запрос чтения параметра и получение ответа
        private COM_data_Out Send_and_Read(SerialPort port, Comand_COM comand, int num_loop)
        {
            COM_data_Out out_data = new COM_data_Out
            {
                need_break = false,
                need_error_procces = false,
                ans = -999
            };
            Log_message?.Invoke(name + " send" + port.PortName + ": " + comand.read_data_COM);
            Log_.Debug(name + " send" + port.PortName + ": " + comand.read_data_COM);
            var a_com_ans = Send_command_to_COM_async(comand.read_data_COM, port);
            if (a_com_ans.Status != TaskStatus.Faulted)
            {
                string com_ans = a_com_ans.Result;
                Log_message?.Invoke(name + " recv" + port.PortName + ": " + com_ans);
                Log_.Debug(name + " recv" + port.PortName + ": " + com_ans);
                if (com_ans != "timeout" && com_ans != "port closed" && com_ans != "")
                {
                    //если получен ответ -парсим данные по ИД команды + пишем в стайт                    
                    out_data = Parse_answer_read(comand, com_ans, num_loop);
                }
                else
                {
                    //Log_message?.Invoke(name + " - timeout or no answer" + port.PortName);
                    out_data.message = name + " - timeout or no answer" + port.PortName;
                    //Error_deal(false, num_loop);
                    out_data.need_error_procces = true;
                }
            }
            else
            {
                //Log_message?.Invoke(name + " - readtask for read was Faulted " + port.PortName);                
                //если данная информация присутствует в стайте, тонужно удалить её
                if (StateInfo.ContainsKey(comand.id))
                {
                    _ = StateInfo.TryRemove(comand.id, out _);
                }
                //Error_deal(false, num_loop);
                out_data.message = name + " - readtask for read was Faulted " + port.PortName;
                out_data.need_error_procces = true;
            }
            return out_data;
        }
        //опрос СОМ
        private void COM_ask(SerialPort port, List<Comand_COM> commands, bool Is_first, int num_loop)
        {
            //счетчик успешных циклов
            int inloop_suc = 0;
            //if (num_loop == 5) Log_message?.Invoke("FUG previos finished = "+finished_previous[num_loop].ToString());
            //берем список команд, опрашиваем каждую из них
            if (port.IsOpen && commands != null && finished_previous[num_loop])
            {
                finished_previous[num_loop] = false;
                foreach (Comand_COM comand in commands)
                {
                    //проверяем нужно ли скипать
                    if (!need_kill_all)
                    {
                        //ИНИ шлется 1 раз в начале цикла (is_first)
                        if ((comand.is_ini && Is_first) || (!comand.is_ini))
                        {
                            //считаем ини комманды
                            if (comand.is_ini && Is_first) ini_comands_count[num_loop] += 1;
                            //шлем команду чтения значения
                            COM_data_Out out_dat = Send_and_Read(port, comand, num_loop);
                            //если нет ошибки
                            if (out_dat.need_break)
                            {
                                Log_message?.Invoke(port.PortName + " critical error, " + out_dat.message);
                                Log_.Error(port.PortName + " critical error, " + out_dat.message);
                                Error_deal(true, num_loop);
                                finished_previous[num_loop] = true;
                                break;
                            }
                            else
                            {
                                //если всё нормально
                                if (!out_dat.need_error_procces)
                                {
                                    //обнуляем счетчик ошибок
                                    //error_count[num_loop] = false;
                                    //считаем успешные опросы
                                    inloop_suc += 1;
                                    //если не ИНИ и не первая, то меняем значение
                                    if (!comand.is_ini && !Is_first && comand.is_write)
                                    {
                                        Check_and_write(out_dat.ans, comand, port);
                                    }
                                }
                                else
                                {
                                    Log_message?.Invoke(port.PortName + " error, " + out_dat.message);
                                    Log_.Error(port.PortName + " error, " + out_dat.message);
                                    Error_deal(false, num_loop);
                                    finished_previous[num_loop] = true;
                                    break;
                                }
                            }
                        }

                    }
                }

                //tесли все комманды из цикал успешно опрошены - значит не было ошибки
                int com_cpunt = commands.Count;
                if (!Is_first) com_cpunt = commands.Count - ini_comands_count[num_loop];
                if (inloop_suc == com_cpunt)
                {
                    error_count[num_loop] = false;
                    finished_previous[num_loop] = true;
                }
            }
        }
        //функция провреки ответа по СОМ
        private COM_data_Out Parse_answer_read(Comand_COM comand, string real_ans, int portname)
        {
            COM_data_Out oout_data = new COM_data_Out { };
            //парсим команды + пишем в стейты
            switch (comand.id)
            {
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                    //это команда ИБП - парсим как ИБП
                    oout_data = Parse_UPS_DQ1(real_ans, portname);
                    break;
                case 10:
                case 12:
                case 13:
                    //это датчик вакуума Thyracont новый протокол
                    oout_data = Parse_Thyracont(real_ans, comand.id, portname);
                    break;
                case 11:
                    //датчик вакуума Thyracont старый протокол
                    oout_data = Parse_Thyracont_old(real_ans, comand.id, portname);
                    break;
                case 14:
                    //FUG set voltage control
                    //oout_data = Parse_FUG_set_control(real_ans,portname);
                    break;
                case 15:
                    //FUG current
                    oout_data = Parse_FUG_cur(real_ans, comand.id, portname);
                    break;
                case 16:
                    //FUG voltage
                    oout_data = Parse_FUG_volt(real_ans, comand.id, portname);
                    break;
                case 18:
                case 19:
                case 20:
                case 21:
                    //DAC I7024
                    oout_data = Parse_DAC_I7024(real_ans, comand.id, portname);
                    break;

                case 23:
                case 24:
                case 25:
                case 26:
                case 27:
                case 28:
                case 29:
                    //Реле
                    oout_data = Parse_M7066(real_ans, comand.id, portname);
                    break;
                case 33:
                    //FUG clear
                    oout_data = Parse_FUG_Clr(real_ans, portname);
                    break;
                case 37:
                case 38:
                case 40:
                case 42:
                case 43:
                case 44:
                case 46:
                case 47:
                case 48:
                case 49:
                case 50:
                case 64:
                case 65:
                case 66:
                case 67:
                case 68:
                case 71:
                case 72:
                case 73:
                case 74:
                case 75:
                case 76:
                case 77:
                case 78:
                case 80:
                case 81:
                case 98:
                case 99:
                case 100:
                case 101:
                case 102:
                    //Pfeiffer 309 Actual speed Hz
                    oout_data = Parse_Pff(real_ans, comand.id, portname);
                    break;
                case 103:
                    //температура LakeShore
                    oout_data = Parse_LakeShore_Temp(real_ans, comand.id, portname);
                    break;
                case 104:
                    //setpoint LakeShore
                    oout_data = Parse_LakeShore_SetPoint(real_ans, comand.id, portname);
                    break;
                case 105:
                    //heater range LakeShore
                    oout_data = Parse_LakeShore_Heater(real_ans, comand.id, portname);
                    break;
                case 109:
                case 110:
                case 111:
                case 112:
                case 113:
                case 114:
                case 115:
                case 116:
                case 117:
                case 118:
                case 119:
                case 120:
                case 121:
                case 122:
                case 123:
                case 124:
                    //DIO I-7045D
                    oout_data = Parse_DIO(real_ans, comand.id, portname); ;
                    break;
            }

            return oout_data;
        }
        //Heater range для LakeShore
        private COM_data_Out Parse_LakeShore_Heater(string ans, int id, int portname)
        {
            COM_data_Out aaa = new COM_data_Out { };
            try
            {
                //Log_.Debug("temperature parse =" +ans.Substring(0, ans.Length - 1).Replace('.', ','));
                //SETP запрос setpoint
                if (float.TryParse(ans.Substring(0, ans.Length).Replace('.', ','), out float temperature))
                {
                    aaa.ans = temperature;
                }
                else
                {
                    aaa.ans = -1;
                }
                Write_to_StateInfo(aaa.ans, id);
            }
            catch (Exception ex)
            {
                aaa.message = name + " parsing problem LakeShore setpoint" + ex.ToString();
                aaa.need_error_procces = true;
            }

            return aaa;
        }
        //setpoint для LakeShore
        private COM_data_Out Parse_LakeShore_SetPoint(string ans, int id, int portname)
        {
            COM_data_Out aaa = new COM_data_Out { };
            try
            {
                //Log_.Debug("temperature parse =" +ans.Substring(0, ans.Length - 1).Replace('.', ','));
                //SETP запрос setpoint
                if (float.TryParse(ans.Substring(0, ans.Length).Replace('.', ','), out float temperature))
                {
                    aaa.ans = temperature;
                }
                else
                {
                    aaa.ans = -1;
                }
                Write_to_StateInfo(aaa.ans, id);
            }
            catch (Exception ex)
            {
                aaa.message = name + " parsing problem LakeShore setpoint" + ex.ToString();
                aaa.need_error_procces = true;
            }

            return aaa;
        }
        //контроллер температуры LakeShore TEMP
        private COM_data_Out Parse_LakeShore_Temp(string ans, int id, int portname)
        {
            COM_data_Out aaa = new COM_data_Out { };
            try
            {
                //Log_.Debug("temperature parse =" +ans.Substring(0, ans.Length - 1).Replace('.', ','));
                //KRDG запрос температуры
                if (float.TryParse(ans.Substring(0, ans.Length).Replace('.', ','), out float temperature))
                {
                    aaa.ans = temperature;
                }
                else
                {
                    aaa.ans = -1;
                }
                Write_to_StateInfo(aaa.ans, id);
            }
            catch (Exception ex)
            {
                aaa.message = name + " parsing problem LakeShore Temperature" + ex.ToString();
                aaa.need_error_procces = true;
            }

            return aaa;
        }
        //DIO I-7045
        private COM_data_Out Parse_DIO(string ans, int id, int portname)
        {
            COM_data_Out aaa = new COM_data_Out { };
            try
            {
                ans = ans.Substring(1, 4);
                int i_ans = int.Parse(ans, System.Globalization.NumberStyles.HexNumber);
                ans = Convert.ToString(i_ans, 2);
                ans = ans.PadLeft(16, '0');
                //в строке всё задом наперед
                char[] rev_ans = ans.ToArray();
                //Log_message?.Invoke("rev_ans = "+rev_ans[15].ToString());

                Array.Reverse(rev_ans);
                //Log_message?.Invoke("aft rev = " + rev_ans[15].ToString());
                //пишем и проверяем значение DIO выходов
                float now_bit = -1;
                switch (id)
                {
                    case 109:
                        now_bit = float.Parse(rev_ans[15].ToString());
                        break;
                    case 110:
                        now_bit = float.Parse(rev_ans[14].ToString());
                        break;
                    case 111:
                        now_bit = float.Parse(rev_ans[13].ToString());
                        break;
                    case 112:
                        now_bit = float.Parse(rev_ans[12].ToString());
                        break;
                    case 113:
                        now_bit = float.Parse(rev_ans[11].ToString());
                        break;
                    case 114:
                        now_bit = float.Parse(rev_ans[10].ToString());
                        break;
                    case 115:
                        now_bit = float.Parse(rev_ans[9].ToString());
                        break;
                    case 116:
                        now_bit = float.Parse(rev_ans[8].ToString());
                        break;
                    case 117:
                        now_bit = float.Parse(rev_ans[7].ToString());
                        break;
                    case 118:
                        now_bit = float.Parse(rev_ans[6].ToString());
                        break;
                    case 119:
                        now_bit = float.Parse(rev_ans[5].ToString());
                        break;
                    case 120:
                        now_bit = float.Parse(rev_ans[4].ToString());
                        break;
                    case 121:
                        now_bit = float.Parse(rev_ans[3].ToString());
                        break;
                    case 122:
                        now_bit = float.Parse(rev_ans[2].ToString());
                        break;
                    case 123:
                        now_bit = float.Parse(rev_ans[1].ToString());
                        break;
                    case 124:
                        now_bit = float.Parse(rev_ans[0].ToString());
                        break;
                }

                //Log_message?.Invoke("DIO "+ id.ToString()+" = "+now_bit.ToString());

                if (!Compare_bits(now_bit, id))
                {
                    // все плохо, параметр выщел за пределы хначения
                    aaa.message = name + " error DIO I7045D value num = " + id.ToString();
                    aaa.need_break = true;
                    //break;
                }
                //aaa.ans=0;
                //Log_message?.Invoke("DIO= "+ans);
                //Log_message?.Invoke("DIO " + id.ToString() + " = " + now_bit.ToString());
            }
            catch (Exception ex)
            {
                aaa.message = name + " parsing problem DIO I-7045" + ex.ToString();
                aaa.need_error_procces = true;
            }
            return aaa;
        }
        //Pfeiffer
        private COM_data_Out Parse_Pff(string ans, int id, int portname)
        {
            COM_data_Out aaa = new COM_data_Out { };
            try
            {
                if (ans.Length >= 16)
                {
                    int dat_len = int.Parse(ans.Substring(8, 2));
                    aaa.ans = float.Parse(ans.Substring(10, dat_len));
                    Log_message?.Invoke("pfeiffer val " + id.ToString() + " = " + aaa.ans);
                    Log_.Debug("pfeiffer val " + id.ToString() + " = " + aaa.ans);
                    Write_to_StateInfo(aaa.ans, id);
                }
            }
            catch (Exception ex)
            {
                aaa.message = name + " parsing problem Pfeiffer" + ex.ToString();
                aaa.need_error_procces = true;
                //Log_message?.Invoke(name + " parsing problem Pfeiffer" + ex.ToString());
                //Error_deal(false, portname);
            }

            return aaa;
        }
        //Парсим I-7024 DAC
        private COM_data_Out Parse_DAC_I7024(string ans, int id, int portname)
        {
            COM_data_Out aaa = new COM_data_Out { };
            try
            {
                if (ans.Length >= 3)
                {
                    aaa.ans = float.Parse(ans.Substring(3, ans.Length - 3).Replace('.', ','));
                    Write_to_StateInfo(aaa.ans, id);
                }
                else
                {
                    aaa.message = name + " some problem DAC I7024";
                    aaa.need_error_procces = true;
                    //Error_deal(false, portname);
                }
            }
            catch (Exception ex)
            {
                //Log_message?.Invoke(name + " parsing problem DAC I7024" + ex.ToString());
                //Error_deal(false, portname);
                aaa.message = name + " parsing problem DAC I7024" + ex.ToString();
                aaa.need_error_procces = true;
            }

            return aaa;
        }
        //Парсим FUG clear
        private COM_data_Out Parse_FUG_Clr(string ans, int portname)
        {
            COM_data_Out aaa = new COM_data_Out { };
            try
            {
                if (ans.Length >= 5 && ans.Substring(0, 5) != "#0 E0")
                {
                    //ошибка
                    aaa.message = name + " FUG no answer for CLR command";
                    //Log_message?.Invoke(name + " FUG no answer for CLR command");
                    //Error_deal(false, portname);
                    aaa.need_error_procces = true;
                }
            }
            catch (Exception ex)
            {
                //Log_message?.Invoke(name + " parsing problem FUG_clr " + ex.ToString());
                //Error_deal(false, portname);
                aaa.message = name + " parsing problem FUG_clr " + ex.ToString();
                aaa.need_error_procces = true;
            }
            return aaa;
        }
        //парсим FUG set control by voltage
        private COM_data_Out Parse_FUG_set_control(string ans, int portname)
        {
            COM_data_Out aaa = new COM_data_Out { };
            try
            {
                if (ans.Length >= 2 && ans.Substring(0, 2) != "E0")
                {
                    //ошибка
                    //Log_message?.Invoke(name + " FUG no answer for set control command");
                    //Error_deal(true, portname);
                    //C_handle();
                    aaa.message = name + " FUG no answer for set control command";
                    aaa.need_error_procces = true;
                }
            }
            catch (Exception ex)
            {
                //Log_message?.Invoke(name + " parsing problem FUG_set_control " + ex.ToString());
                //Error_deal(false, portname);
                aaa.message = name + " parsing problem FUG_set_control " + ex.ToString();
                aaa.need_error_procces = true;
            }
            return aaa;
        }
        //Парсим напряжение FUG
        private COM_data_Out Parse_FUG_cur(string ans, int id, int portname)
        {
            COM_data_Out aaa = new COM_data_Out { };
            try
            {
                //Log_message?.Invoke("current = " + ans.Substring(6, ans.Length - 6));
                if (ans.Length > 6)
                {
                    aaa.ans = float.Parse(ans.Substring(3, ans.Length - 3).Replace('.', ','));
                    Write_to_StateInfo(aaa.ans, id);
                }
                else
                {
                    //Error_deal(false, portname);
                    aaa.message = name + " some problem FUG current";
                    aaa.need_error_procces = true;
                }
            }
            catch (Exception ex)
            {
                //Log_message?.Invoke(name + " parsing problem FUG current"+ ex.ToString());
                //Error_deal(false, portname);
                aaa.message = name + " parsing problem FUG current" + ex.ToString();
                aaa.need_error_procces = true;
            }
            return aaa;
        }
        //Парсим напряжение FUG
        private COM_data_Out Parse_FUG_volt(string ans, int id, int portname)
        {
            COM_data_Out aaa = new COM_data_Out { };
            try
            {
                if (ans.Length > 6)
                {
                    //Log_message?.Invoke("voltage = " + ans.Substring(6, ans.Length - 6));
                    aaa.ans = float.Parse(ans.Substring(3, ans.Length - 3).Replace('.', ','));
                    Write_to_StateInfo(aaa.ans, id);
                }
                else
                {
                    //Log_message?.Invoke(name + " some problem FUG voltage");
                    //Error_deal(false, portname);
                    aaa.message = name + " some problem FUG voltage";
                    aaa.need_error_procces = true;
                }
            }
            catch (Exception ex)
            {
                //Log_message?.Invoke(name + " parsing problem FUG voltage" + ex.ToString());
                //Error_deal(false, portname);
                aaa.message = name + " some problem FUG voltage" + ex.ToString();
                aaa.need_error_procces = true;
            }
            return aaa;
        }
        /// <summary>
        /// проверяем можно ли менять значение параметра
        /// </summary>
        /// <param name="id_comand"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool Can_change(int id_comand, float value)
        {
            bool ans = false;
            switch (id_comand)
            {
                case 40:
                    //выключать LL ТМН только при 23 = 0 (закрыт клапан LL)
                    if (value == 0 && StateInfo.TryGetValue(23, out float val40))
                    {
                        if (val40 == 0)
                        {
                            ans = true;
                        }
                    }
                    else ans = true;
                    break;
                case 74:
                    //выключать AC ТМН только при 24 = 0 (закрыт клапан AC)
                    if (value == 0 && StateInfo.TryGetValue(24, out float val74))
                    {
                        if (val74 == 0)
                        {
                            ans = true;
                        }
                    }
                    else ans = true;
                    break;
                case 28:
                    //выключать форвакуумныей LL только при закрытом клапане (23 = 0)
                    if (value == 0 && StateInfo.TryGetValue(23, out float val28))
                    {
                        if (val28 == 0)
                        {
                            ans = true;
                        }
                    }
                    else ans = true;
                    break;
                case 29:
                    //выключать форвакуумныей AC только при закрытом клапане (24 = 0)
                    if (value == 0 && StateInfo.TryGetValue(24, out float val29))
                    {
                        if (val29 == 0)
                        {
                            ans = true;
                        }
                    }
                    else ans = true;
                    break;
                default:
                    ans = true;
                    break;
            }

            return ans;
        }

        //проверка нужно ли изменение параметра
        private void Check_and_write(float ans, Comand_COM com, SerialPort port)
        {
            try
            {
                //проверяем есть ли в списке на изменение данная команда
                if (Push_target_list.ContainsKey(com.id))
                {
                    if (max_min_tar_data.TAR_data_slice.ContainsKey(com.id))
                    {
                        //обновляем значение таргет
                        max_min_tar_data.TAR_data_slice[com.id] = Push_target_list[com.id];
                    }
                    else
                    {
                        max_min_tar_data.TAR_data_slice.TryAdd(com.id,Push_target_list[com.id]);
                    }
                    //удаляем из списка
                    Push_target_list.TryRemove(com.id, out _);
                }
                //сравниваем текущее значение и макс/мин
                if (ans >= max_min_tar_data.MIN_data_slice[com.id] &&
                    ans <= max_min_tar_data.MAX_data_slice[com.id])
                {
                    //если в переделах нормы - сравниваем нужно ли менять значение
                    if (max_min_tar_data.TAR_data_slice.ContainsKey(com.id) && ans < max_min_tar_data.TAR_data_slice[com.id])
                    {
                        //если шаг не выйдет за пределы макс/мин                    
                        if ((ans + max_min_tar_data.Step_plus[com.id]) <= max_min_tar_data.MAX_data_slice[com.id] &&
                                (ans + max_min_tar_data.Step_plus[com.id]) <= max_min_tar_data.TAR_data_slice[com.id])
                        {
                            //проверяем можно ли делать шаг, если да - делаем
                            if (Can_change(com.id, ans + max_min_tar_data.Step_plus[com.id]))
                            {
                                //делаем шаг вперед
                                COM_write(com, port, ans + max_min_tar_data.Step_plus[com.id]);
                            }
                            else
                            {
                                Log_.Debug("param "+ com.id.ToString() + " change not allowed");
                            }
                        }
                    }
                    else
                    {
                        if (max_min_tar_data.TAR_data_slice.ContainsKey(com.id) && ans > max_min_tar_data.TAR_data_slice[com.id])
                            //если шаг не выйдет за пределы макс/мин
                            if ((ans - max_min_tar_data.Step_minus[com.id]) >= max_min_tar_data.MIN_data_slice[com.id] &&
                                        (ans - max_min_tar_data.Step_minus[com.id]) >= max_min_tar_data.TAR_data_slice[com.id])
                            {
                                //проверяем можно ли делать шаг, если да - делаем
                                if (Can_change(com.id, ans - max_min_tar_data.Step_minus[com.id]))
                                {
                                    //делаем шаг назад
                                    COM_write(com, port, ans - max_min_tar_data.Step_minus[com.id]);
                                }
                                else
                                {
                                    Log_.Debug("param " + com.id.ToString() + " change not allowed");
                                }
                            }
                    }
                }
                else
                {
                    //пытаемся вернуться к целевым занчениям
                    if (ans < max_min_tar_data.MIN_data_slice[com.id])
                    {
                        //делаем шаг вперед
                        COM_write(com, port, ans + max_min_tar_data.Step_plus[com.id]);
                    }
                    else
                    {
                        if (ans > max_min_tar_data.MAX_data_slice[com.id])
                            COM_write(com, port, ans - max_min_tar_data.Step_minus[com.id]);

                    }
                }
            }
            catch (Exception ex)
            {
                Log_.Error("Some error in block Check_and_write "+ ex.ToString());
            }
        }
        //проверка ответа ИБП
        private COM_data_Out Compare_UPS_DQ1(UPS_data real_d, int portname)
        {
            COM_data_Out aaa = new COM_data_Out
            {
                message = ""
            };
            Write_to_StateInfo(real_d.i_p_voltage, 1);
            Write_to_StateInfo(real_d.i_p_f_voltage, 2);
            Write_to_StateInfo(real_d.o_p_voltage, 3);
            Write_to_StateInfo(real_d.o_p_current, 4);
            Write_to_StateInfo(real_d.i_p_frequency, 5);
            Write_to_StateInfo(real_d.Bat_level, 6);
            Write_to_StateInfo(real_d.temperature, 7);
            Write_to_StateInfo(real_d.ups_fault, 8);
            if (real_d.i_p_voltage >= max_min_tar_data.MIN_data_slice[1] &&
                real_d.i_p_voltage <= max_min_tar_data.MAX_data_slice[1])
            {
                if (real_d.i_p_f_voltage >= max_min_tar_data.MIN_data_slice[2] &&
                    real_d.i_p_f_voltage <= max_min_tar_data.MAX_data_slice[2])
                {
                    if (real_d.o_p_voltage >= max_min_tar_data.MIN_data_slice[3] &&
                        real_d.o_p_voltage <= max_min_tar_data.MAX_data_slice[3])
                    {
                        if (real_d.o_p_current >= max_min_tar_data.MIN_data_slice[4] &&
                            real_d.o_p_current <= max_min_tar_data.MAX_data_slice[4])
                        {
                            if (real_d.i_p_frequency >= max_min_tar_data.MIN_data_slice[5] &&
                                real_d.i_p_frequency <= max_min_tar_data.MAX_data_slice[5])
                            {
                                if (real_d.Bat_level >= max_min_tar_data.MIN_data_slice[6] &&
                                    real_d.Bat_level <= max_min_tar_data.MAX_data_slice[6])
                                {
                                    if (real_d.temperature >= max_min_tar_data.MIN_data_slice[7] &&
                                        real_d.temperature <= max_min_tar_data.MAX_data_slice[7])
                                    {
                                        if (real_d.ups_fault >= max_min_tar_data.MIN_data_slice[8] &&
                                            real_d.ups_fault <= max_min_tar_data.MAX_data_slice[8])
                                        {
                                            aaa.need_break = false;
                                            aaa.need_error_procces = false;
                                        }
                                        else aaa.message = "Faul/Utilityfail/Lowbattery/BypassON - out of range";
                                    }
                                    else aaa.message = "Temperature - out of range";
                                }
                                else aaa.message = "Battery level - out of range";
                            }
                            else aaa.message = "IP frequency - out of range";
                        }
                        else aaa.message = "OP current - out of range";
                    }
                    else aaa.message = "OP voltage - out of range";
                }
                else aaa.message = "IPF voltage - out of range";
            }
            else aaa.message = "IP voltage - out of range";
            if (aaa.message != "")
            {
                //Log_message?.Invoke(name + " "+ aaa.message);
                //Error_deal(true, portname);
                aaa.need_break = true;
                aaa.message = name + " " + aaa.message;
            }
            return aaa;
        }
        //парсинг ответа датчика вакуума
        private COM_data_Out Parse_Thyracont(string com_ans, int id, int portname)
        {
            COM_data_Out aaa = new COM_data_Out { };
            try
            {
                if (com_ans.Length > 10)
                {
                    //TODO дописать проверки для неверные данные
                    if (com_ans.Substring(3, 1) != "7")
                    {
                        int data_len = int.Parse(com_ans.Substring(7, 1));
                        Thyr_data Thyr_real_data = new Thyr_data
                        {
                            //pressure = float.Parse(com_ans.Substring(8, data_len))
                            pressure = float.Parse(com_ans.Substring(8, data_len).Replace('.', ','))
                        };
                        Write_to_StateInfo(Thyr_real_data.pressure, id);

                        if (Thyr_real_data.pressure >= max_min_tar_data.MIN_data_slice[id] && Thyr_real_data.pressure <= max_min_tar_data.MAX_data_slice[id])
                        {
                            aaa.ans = Thyr_real_data.pressure;
                        }
                        else
                        {
                            //Log_message?.Invoke(name + " - недопустимое давление на датчике" + id.ToString());
                            //Error_deal(true, portname);
                            aaa.message = name + " -  BAD pressure!!! " + id.ToString() + "pressure = " + Thyr_real_data.pressure.ToString();
                            aaa.need_error_procces = true;
                            aaa.need_break = true;
                        }
                    }
                }
            }
            catch (Exception exx)
            {
                //Log_message?.Invoke(name + " - Thyracont parsing problem " + exx.Message.ToString());
                //Error_deal(false, portname);
                aaa.message = name + " - Thyracont parsing problem " + exx.Message.ToString();
                aaa.need_error_procces = true;
            }
            return aaa;
        }
        //парсинг ответа датчика вакуума, старый протокол
        private COM_data_Out Parse_Thyracont_old(string com_ans, int id, int portname)
        {
            COM_data_Out aaa = new COM_data_Out { };
            try
            {
                if (com_ans.Length > 10)
                {
                    //TODO дописать проверки для неверные данные
                    //if (com_ans.Substring(3, 1) != "7")
                    //{
                    //int data_len = int.Parse(com_ans.Substring(7, 1));
                    Thyr_data Thyr_real_data = new Thyr_data
                    {
                        //pressure = float.Parse(com_ans.Substring(8, data_len))
                        pressure = float.Parse(com_ans.Substring(4, 4))
                    };
                    //Log_message?.Invoke("pressure = " +Thyr_real_data.pressure.ToString());
                    int ggg = int.Parse(com_ans.Substring(8, 2));
                    //Log_message?.Invoke("exp = " + ggg.ToString());
                    //Log_message?.Invoke("pow = " + Math.Pow(10, ggg - 20 - 3).ToString());
                    Thyr_real_data.pressure = Thyr_real_data.pressure * Convert.ToSingle(Math.Pow(10, ggg - 20 - 3));
                    //Log_message?.Invoke("res = " + Thyr_real_data.pressure.ToString());
                    Write_to_StateInfo(Thyr_real_data.pressure, id);

                    if (Thyr_real_data.pressure >= max_min_tar_data.MIN_data_slice[id] && Thyr_real_data.pressure <= max_min_tar_data.MAX_data_slice[id])
                    {
                        aaa.ans = Thyr_real_data.pressure;
                    }
                    else
                    {
                        //Log_message?.Invoke(name + " - недопустимое давление на датчике" + id.ToString());
                        //Error_deal(true, portname);
                        aaa.message = name + " -  BAD pressure!!! " + id.ToString() + "pressure = " + Thyr_real_data.pressure.ToString();
                        aaa.need_error_procces = true;
                        aaa.need_break = true;
                    }
                    //}
                }
            }
            catch (Exception exx)
            {
                //Log_message?.Invoke(name + " - Thyracont parsing problem " + exx.Message.ToString());
                //Error_deal(false, portname);
                aaa.message = name + " - Thyracont parsing problem " + exx.Message.ToString();
                aaa.need_error_procces = true;
            }
            return aaa;
        }
        //Парсинг ответа ИБП
        private COM_data_Out Parse_UPS_DQ1(string COM_ans, int portname)
        {
            COM_data_Out aaa = new COM_data_Out { };
            //парсим строку состояния ИБП
            //фнукция вернет True если всё нормально
            if (COM_ans.Length > 38)
            {
                try
                {
                    //TODO запихнуть в try
                    UPS_data UPC_real_data = new UPS_data
                    {
                        i_p_voltage = float.Parse(COM_ans.Substring(1, 5).Replace('.', ',')),
                        //i_p_voltage = float.Parse(COM_ans.Substring(1, 5)),
                        i_p_f_voltage = float.Parse(COM_ans.Substring(7, 5).Replace('.', ',')),
                        //i_p_f_voltage = float.Parse(COM_ans.Substring(7, 5)),
                        o_p_voltage = float.Parse(COM_ans.Substring(13, 5).Replace('.', ',')),
                        //o_p_voltage = float.Parse(COM_ans.Substring(13, 5)),
                        o_p_current = int.Parse(COM_ans.Substring(19, 3)),
                        i_p_frequency = float.Parse(COM_ans.Substring(23, 4).Replace('.', ',')),
                        //i_p_frequency = float.Parse(COM_ans.Substring(23, 4)),
                        Bat_level = int.Parse(COM_ans.Substring(28, 4)),
                        temperature = float.Parse(COM_ans.Substring(33, 4).Replace('.', ',')),
                        //temperature = float.Parse(COM_ans.Substring(33, 4)),
                        ups_fault = int.Parse(COM_ans.Substring(38, 4))
                    };
                    //Log_mes?.Invoke("A - test 5");
                    aaa = Compare_UPS_DQ1(UPC_real_data, portname);
                }
                catch (FormatException exx)
                {
                    //Log_message?.Invoke(name + " - parsing errror " + exx.Message.ToString());
                    //Error_deal(false, portname);
                    aaa.message = name + " UPS parsing errror " + exx.Message.ToString();
                    aaa.need_error_procces = true;
                }
                catch (Exception ex)
                {
                    //Log_message?.Invoke(name + " - UPS comparison error" + ex.Message.ToString());
                    //Error_deal(false, portname);
                    aaa.message = name + " UPS some error " + ex.Message.ToString();
                    aaa.need_error_procces = true;
                }
            }
            return aaa;
        }
        //сравниваем и пишем биты реле М-7066
        private bool Compare_bits(float bit_now, int i)
        {
            Write_to_StateInfo(bit_now, i);
            if (bit_now >= max_min_tar_data.MIN_data_slice[i] &&
                bit_now <= max_min_tar_data.MAX_data_slice[i])
            {
                return true;
            }
            else return false;
        }
        //парсинг ответа М-7066
        private COM_data_Out Parse_M7066(string ans, int tar_id, int portname)
        {
            COM_data_Out aaa = new COM_data_Out { };
            //HEX - in int
            //int in binary string
            try
            {
                ans = ans.Substring(1, 2);
                int i_ans = int.Parse(ans, System.Globalization.NumberStyles.HexNumber);
                ans = Convert.ToString(i_ans, 2);
                ans = ans.PadLeft(7, '0');
                //в строке всё задом наперед
                int m = 29;
                for (int i = 23; i <= 29; i++)
                {
                    if (Compare_bits(float.Parse(ans.Substring(i - 23, 1)), m))
                    {
                        if (tar_id == m) aaa.ans = float.Parse(ans.Substring(i - 23, 1));
                    }
                    else
                    {
                        //Error_deal(true, portname);
                        //break;
                        aaa.message = name + " error M7066 value num = " + i.ToString();
                        aaa.need_break = true;
                        break;
                    }
                    m--;
                }
            }
            catch (FormatException ex)
            {
                aaa.ans = -999;
                //Log_message?.Invoke(name + " - parsing M7066 error "+ans+"\n" + ex.Message.ToString());
                aaa.message = name + " - parsing M7066 error " + ans + "\n" + ex.Message.ToString();
                aaa.need_error_procces = true;
            }
            return aaa;
        }
        //запись ответа в информацию о состоянии
        private bool Write_to_StateInfo(float ans, int comID)
        {
            bool res = false;
            try
            {
                if (StateInfo.ContainsKey(comID))
                {
                    StateInfo[comID] = ans;
                }
                else
                {
                    StateInfo.TryAdd(comID, ans);
                }
                res = true;
            }
            catch (Exception ex)
            {
                Log_message?.Invoke(name + " - Write_to_StateInfo error " + comID.ToString() + "\n Exception = " + ex.ToString());
                Log_.Error(name + " - Write_to_StateInfo error " + comID.ToString() + "\n Exception = " + ex.ToString());
            }
            return res;
        }
        //тупо задача чтения из порта
        private Task<string> ReadTask(SerialPort port)
        {
            string message;
            try
            {
                if (port.IsOpen)
                {
                    message = port.ReadLine();
                }
                else message = "port closed";
            }
            catch (TimeoutException)
            {
                message = "timeout";
            }
            return Task.FromResult(message);
        }
        //посылка команды СОМ и получение ответа с использованием await TASK
        private async Task<string> Send_command_to_COM_async(string comand, SerialPort port)
        {
            if (port.IsOpen)
            {
                port.DiscardInBuffer();
                string ans_message_com;
                port.WriteLine(comand);
                ans_message_com = await ReadTask(port);
                return ans_message_com;
            }
            else return "";
        }
        //конструктор строки + команда на запись + проверка ответа
        private void COM_write(Comand_COM com, SerialPort port, float data)
        {
            //конструируем строку ответа
            string cc = Command_construct(com, data.ToString());
            //шлем команду на запись
            Log_.Debug(name + " send" + port.PortName + ": " + cc);
            Log_message?.Invoke(name + " send" + port.PortName + ": " + cc);
            //string com_ans = Send_command_to_COM(cc, port, com.is_cr);
            var a_com_ans = Send_command_to_COM_async(cc, port);
            if (a_com_ans.Status != TaskStatus.Faulted)
            {
                string com_ans = a_com_ans.Result;
                Log_.Debug(name + " rcv" + port.PortName + ": " + com_ans);
                Log_message?.Invoke(name + " rcv" + port.PortName + ": " + com_ans);
                //проверяем ответ на команду записи
                //TODO
            }
            else
            {
                //TODO обработка ошибки
                Log_message?.Invoke(name + " - readtask for write was Faulted " + port.PortName);
                Log_.Error(name + " - readtask for write was Faulted " + port.PortName);
            }
        }
        //расчет контрольной суммы для Pfeiffer
        private int Check_sum(string str)
        {
            int chksum = 0;
            Byte[] asciibytes = Encoding.ASCII.GetBytes(str);
            foreach (Byte bt in asciibytes)
            {
                chksum = chksum + int.Parse(bt.ToString());
            }
            chksum = chksum % 256;
            return chksum;
        }
        //конструированеи окмманды для записи
        private string Command_construct(Comand_COM ccc, string data)
        {
            string ans = "";
            switch (ccc.id)
            {
                case 15:
                    //FUG current
                    ans = ccc.prefix + data;
                    ans = ans.Replace(',', '.');
                    break;
                case 16:
                    //FUG voltage
                    ans = ccc.prefix + data;
                    ans = ans.Replace(',', '.');
                    break;
                case 18:
                case 19:
                case 20:
                case 21:
                    float.TryParse(data, out float a);
                    if (a >= 0)
                    {
                        ans = ccc.prefix + "+" + a.ToString("00.000");
                    }
                    else ans = ccc.prefix + a.ToString("00.000");
                    ans = ans.Replace(',', '.');
                    break;
                case 23:
                case 24:
                case 25:
                case 26:
                case 27:
                case 28:
                case 29:
                    //это М-7066
                    ans = ccc.prefix + data + ccc.postfix;
                    break;
                case 40:
                    int.TryParse(data, out int val1);
                    ans = ccc.prefix + val1.ToString("000000");
                    int ch1 = Check_sum(ans);
                    ans = ans + ch1.ToString("000");
                    break;
                case 42:
                    //Pfeiffer
                    //+значение + сrc
                    //Log_message?.Invoke("test data= " + data);
                    int.TryParse(data, out int val);
                    ans = ccc.prefix + val.ToString("000");
                    //Log_message?.Invoke("test ans = "+ans);
                    int ch = Check_sum(ans);
                    ans = ans + ch.ToString("000");
                    //Log_message?.Invoke("test ans full = " + ans);
                    break;
                case 104:
                    //SETTEMP
                    //float.TryParse(data, out float settemp);
                    ans = ccc.prefix + data;
                    break;
                case 105:
                    //Heater Range
                    //int.TryParse(data, out int heater);
                    ans = ccc.prefix + data;
                    break;
            }

            return ans;
        }
        //убиваем один таймер
        private bool Kill_One_timer(System.Threading.Timer timer, SerialPort port, AutoResetEvent evnt)
        {
            if (timer != null)
            {
                evnt.WaitOne();
                timer.Dispose();
                port.Close();
                port.Dispose();
                return true;
            }
            else return false;
        }
        //убиваем все запущенные таймеры
        public void Kill_all_timers()
        {
            if (Kill_One_timer(COM1_timer, _serialPort_1, event_1))
            {
                Log_message?.Invoke(name + " timer 1 killed");
                Log_.Debug(name + " timer 1 killed");
            }
            if (Kill_One_timer(COM2_timer, _serialPort_2, event_2))
            {
                Log_message?.Invoke(name + " timer 2 killed");
                Log_.Error(name + " timer 2 killed");
            }
            if (Kill_One_timer(COM3_timer, _serialPort_3, event_3))
            {
                Log_message?.Invoke(name + " timer 3 killed");
                Log_.Error(name + " timer 3 killed");
            }
            if (Kill_One_timer(COM4_timer, _serialPort_4, event_4))
            {
                Log_message?.Invoke(name + " timer 4 killed");
                Log_.Error(name + " timer 4 killed");
            }
            if (Kill_One_timer(COM5_timer, _serialPort_5, event_5))
            {
                Log_message?.Invoke(name + " timer 5 killed");
                Log_.Error(name + " timer 5 killed");
            }
            if (Kill_One_timer(COM6_timer, _serialPort_6, event_6))
            {
                Log_message?.Invoke(name + " timer 6 killed");
                Log_.Error(name + " timer 6 killed");
            }
            if (Kill_One_timer(COM7_timer, _serialPort_7, event_7))
            {
                Log_message?.Invoke(name + " timer 7 killed");
                Log_.Error(name + " timer 7 killed");
            }
            if (Kill_One_timer(COM8_timer, _serialPort_8, event_8))
            {
                Log_message?.Invoke(name + " timer 8 killed");
                Log_.Error(name + " timer 8 killed");
            }
            //останавливаем TCP
            //if (TCP_timer!=null)
            //{
            //event_TCP.WaitOne();
            //TCP_timer.Dispose();
            //}
            GC.Collect();
        }
        //разбираемся с ошибками
        private void Error_deal(bool is_critical, int portnum)
        {
            //если критическая ошибка
            if (is_critical)
            {
                Er_handle();
            }
            else
            {
                //error-count= false если не было ошибок
                if (error_count[portnum])
                {
                    Log_message?.Invoke("second error in port" + portnum.ToString());
                    Log_.Error("second error in port" + portnum.ToString());
                    //error_count[portnum] = false;
                    Er_handle();
                }
                else
                {
                    Log_message?.Invoke("first error - wait next, port = " + portnum.ToString());
                    Log_.Error("first error - wait next, port = " + portnum.ToString());
                    error_count[portnum] = true;
                    //skip_flag[portnum] = true;
                }
            }
        }
        /// <summary>
        /// проверяем лежит ли значение параметра в пределах от таргет +- степ
        /// </summary>
        /// <returns></returns>
        public bool Check_target_reached()
        {
            bool ans = true;
            //false если какой-то из параметров не достиг целевого значения
            //true если можно двигаться
            //берем каждый параметр в словаре целевых значений
            foreach (KeyValuePair<int, float> kv in max_min_tar_data.TAR_data_slice)
            {
                if (StateInfo.ContainsKey(kv.Key) && max_min_tar_data.Step_minus.ContainsKey(kv.Key) && max_min_tar_data.Step_plus.ContainsKey(kv.Key))
                {
                    //Log_message?.Invoke("stateinf="+StateInfo[kv.Key].ToString());
                    //Log_message?.Invoke("target  =" + kv.Value);
                    //если зн больше таргета, то сравниваем с шагом-
                    if (StateInfo[kv.Key] > kv.Value)
                    {
                        if (StateInfo[kv.Key] - kv.Value >= max_min_tar_data.Step_minus[kv.Key])
                        {
                            ans = false;
                            Log_.Debug("Key " + kv.Key.ToString() + " not riched target (now = " + kv.Value.ToString() + ")");
                        }
                    }
                    //если зн меньше таргета, то сравниваем с шагом+
                    if (StateInfo[kv.Key] < kv.Value)
                    {
                        if (kv.Value - StateInfo[kv.Key] >= max_min_tar_data.Step_plus[kv.Key])
                        {
                            ans = false;
                            Log_.Debug("Key " + kv.Key.ToString() + " not riched target (now = " + kv.Value.ToString() + ")");
                        }
                    }
                }
                else
                {
                    //Log_message?.Invoke("no some keys = " + kv.Key.ToString());
                    Log_.Error("no some keys = " + kv.Key.ToString());
                    ans = false;
                }
            }
            return ans;
        }

        public void SetContext(State_Machine context)
        {
            _context = context;
        }
        //safe state
        public abstract void SS_handle();
        //pump Low Load Lock
        public abstract void PLLL_handle();
        //pump High Load Lock
        public abstract void PHLL_handle();
        //Error
        public abstract void Er_handle();
        //Emergency
        public abstract void Em_handle();
        //Open LoadLock
        public abstract void ALL_handle();
        //High Vacuum Stand By
        public abstract void HVSB_handle();
        //Analisys state
        public abstract void AN_handle();

    }

    public class SS_state : State
    {
        private event Log_Handler Log_mes;
        public SS_state(Log_Handler Logger, State_data s_data)
        {
            State_data SS_st_data = s_data;
            //if (_context._Data != null)
            //SS_st_data = _context._Data.st_SS;
            this.Log_message += Logger;
            Log_mes = Logger;
            this.name = "SS";
            Log_mes?.Invoke("State " + this.name + " was created");
            Log_.Debug("State " + this.name + " was created");
            this.MAXMIN_version = SS_st_data.MAX_MIN_version;
            //this.IP_adress = State_Machine._Data.IP_adress_port;
            need_kill_all = false;
            //грузим списки комманд для разных СОМов
            List<Comand_COM> comm_1_list = SS_st_data.list1;
            List<Comand_COM> comm_2_list = SS_st_data.list2;
            List<Comand_COM> comm_3_list = SS_st_data.list3;
            List<Comand_COM> comm_4_list = SS_st_data.list4;
            List<Comand_COM> comm_5_list = SS_st_data.list5;
            List<Comand_COM> comm_6_list = SS_st_data.list6;
            List<Comand_COM> comm_7_list = SS_st_data.list7;
            List<Comand_COM> comm_8_list = SS_st_data.list8;
            //ТОВО загрузить их или получить из аргументов функции

            //зпускаем тайемры + передаем настройки
            RunTimer(comm_1_list,
                     comm_2_list,
                     comm_3_list,
                     comm_4_list,
                     comm_5_list,
                     comm_6_list,
                     comm_7_list,
                     comm_8_list,
                     SS_st_data.timers_set,
                     SS_st_data.com_settings,
                     SS_st_data.max_min_target);
            //Log_mes?.Invoke("State "+this.name +" was created");
            //Log_mes?.Invoke(this.name);
        }
        public override void SS_handle()
        {
            Log_mes?.Invoke("SS - do nothing");
            Log_.Debug("SS - do nothing");
        }
        public override void PHLL_handle()
        {
            Log_mes?.Invoke("PHLL - forbidden - do nothing");
            Log_.Debug("PHLL - forbidden - do nothing");
        }
        public override void PLLL_handle()
        {
            //проврека достигнуты ли целевые параметры
            if (Check_target_reached())
            {
                need_kill_all = true;
                Kill_all_timers();
                Log_mes?.Invoke("SS  - Go to PLLL");
                Log_.Debug("SS  - Go to PLLL");
                //_context._Data.st_PLLL.max_min_target.TAR_data_slice = _context._reserved.st_PLLL.max_min_target.TAR_data_slice;
                _context._Data.st_PLLL.max_min_target.TAR_data_slice = _context.Got_target_list_default("PLLL", _context.settings_path);
                _context.TransitionTo(new PLLL_state(new Log_Handler(Log_mes), _context._Data.st_PLLL));
                GC.Collect();
            }
            else
            {
                Log_mes?.Invoke("SS  - not all targets reached");
                Log_.Debug("SS  - not all targets reached");
            }
        }
        public override void Er_handle()
        {
            //без проверки на достижение целевых параметров
            need_kill_all = true;
            Kill_all_timers();
            Log_mes?.Invoke("SS  - Go to Er");
            Log_.Debug("SS  - Go to Er");
            //_context._Data.st_Er.max_min_target.TAR_data_slice = _context._reserved.st_Er.max_min_target.TAR_data_slice;
            _context._Data.st_Er.max_min_target.TAR_data_slice = _context.Got_target_list_default("Er", _context.settings_path);
            _context.TransitionTo(new Er_state(new Log_Handler(Log_mes), _context._Data.st_Er));
            GC.Collect();
        }
        public override void Em_handle()
        {
            //без проверки на достижение целевых параметров
            need_kill_all = true;
            Kill_all_timers();
            Log_mes?.Invoke("SS  - Go to Em");
            Log_.Debug("SS  - Go to Em");
            //_context._Data.st_Em.max_min_target.TAR_data_slice = _context._reserved.st_Em.max_min_target.TAR_data_slice;
            _context._Data.st_Em.max_min_target.TAR_data_slice = _context.Got_target_list_default("Em", _context.settings_path);
            _context.TransitionTo(new Em_state(new Log_Handler(Log_mes), _context._Data.st_Em));
            GC.Collect();
        }
        public override void ALL_handle()
        {
            Log_.Debug(this.name + " - Open Load Lock fobidden - do nothing");
        }
        public override void AN_handle()
        {
            Log_.Debug(this.name + " - Analysis fobidden - do nothing");
        }
        public override void HVSB_handle()
        {
            Log_.Debug(this.name + " - High vaccum Stand By fobidden - do nothing");
        }
    }
    public class Er_state : State
    {
        public event Log_Handler Log_mes;
        public Er_state(Log_Handler Logger, State_data s_data)
        {
            //делаем словарь команд
            State_data Er_st_data = s_data;
            //if (_context._Data != null)
            //Er_st_data = _context._Data.st_Er;            
            this.Log_message += Logger;
            Log_mes = Logger;
            //Log_message("");
            this.name = "Er";
            Log_mes?.Invoke("State " + this.name + " was created");
            Log_.Debug("State " + this.name + " was created");
            this.MAXMIN_version = Er_st_data.MAX_MIN_version;
            need_kill_all = false;
            //грузим списки комманд для разных СОМов
            List<Comand_COM> comm_1_list = Er_st_data.list1;
            List<Comand_COM> comm_2_list = Er_st_data.list2;
            List<Comand_COM> comm_3_list = Er_st_data.list3;
            List<Comand_COM> comm_4_list = Er_st_data.list4;
            List<Comand_COM> comm_5_list = Er_st_data.list5;
            List<Comand_COM> comm_6_list = Er_st_data.list6;
            List<Comand_COM> comm_7_list = Er_st_data.list7;
            List<Comand_COM> comm_8_list = Er_st_data.list8;
            //ТОВО загрузить их или получить из аргументов функции

            //зпускаем тайемры + передаем настройки
            RunTimer(comm_1_list,
                     comm_2_list,
                     comm_3_list,
                     comm_4_list,
                     comm_5_list,
                     comm_6_list,
                     comm_7_list,
                     comm_8_list,
                     Er_st_data.timers_set,
                     Er_st_data.com_settings,
                     Er_st_data.max_min_target);
        }
        public override void SS_handle()
        {
            need_kill_all = true;
            Kill_all_timers();
            Log_mes?.Invoke("Er  - Go to SS");
            Log_.Debug("Er  - Go to SS");
            //_context._Data.st_SS.max_min_target.TAR_data_slice = _context._reserved.st_SS.max_min_target.TAR_data_slice;
            _context._Data.st_SS.max_min_target.TAR_data_slice = _context.Got_target_list_default("SS", _context.settings_path);
            //Log_.Trace(_context._Data.st_SS.max_min_target.TAR_data_slice[16].ToString());
            _context.TransitionTo(new SS_state(new Log_Handler(Log_mes), _context._Data.st_SS));
            GC.Collect();
        }
        public override void Er_handle()
        {
            Log_mes?.Invoke("Er - do nothing");
            Log_.Debug("Er - do nothing");
        }
        public override void PLLL_handle()
        {
            Log_mes?.Invoke("Er - PLLL is forbidden - do nothing");
            Log_.Debug("Er - PLLL is forbidden - do nothing");
        }
        public override void Em_handle()
        {
            need_kill_all = true;
            Kill_all_timers();
            Log_mes?.Invoke("Er  - Go to Em");
            Log_.Debug("Er  - Go to Em");
            //_context._Data.st_Em.max_min_target.TAR_data_slice = _context._reserved.st_Em.max_min_target.TAR_data_slice;
            _context._Data.st_Em.max_min_target.TAR_data_slice = _context.Got_target_list_default("Em", _context.settings_path);
            _context.TransitionTo(new Em_state(new Log_Handler(Log_mes), _context._Data.st_Em));
            GC.Collect();
        }
        public override void PHLL_handle()
        {
            Log_mes?.Invoke("Er - PHLL is forbidden - do nothing");
            Log_.Debug("Er - PHLL is forbidden - do nothing");
        }
        public override void ALL_handle()
        {
            Log_.Debug(this.name + " - Open Load Lock fobidden - do nothing");
        }
        public override void AN_handle()
        {
            Log_.Debug(this.name + " - Analysis fobidden - do nothing");
        }
        public override void HVSB_handle()
        {
            Log_.Debug(this.name + " - High vaccum Stand By fobidden - do nothing");
        }
    }
        public class Em_state : State
        {
            public event Log_Handler Log_mes;
            public Em_state(Log_Handler Logger, State_data s_data)
            {
                //делаем словарь команд
                State_data Em_st_data = s_data;
                //if (_context != null)
                //Em_st_data = _context._Data.st_Em;
                this.Log_message += Logger;
                Log_mes = Logger;
                //Log_message("");
                this.name = "Em";
                Log_mes?.Invoke("State " + this.name + " was created");
                Log_.Debug("State " + this.name + " was created");
                this.MAXMIN_version = Em_st_data.MAX_MIN_version;
                need_kill_all = false;
                //грузим списки комманд для разных СОМов
                List<Comand_COM> comm_1_list = Em_st_data.list1;
                List<Comand_COM> comm_2_list = Em_st_data.list2;
                List<Comand_COM> comm_3_list = Em_st_data.list3;
                List<Comand_COM> comm_4_list = Em_st_data.list4;
                List<Comand_COM> comm_5_list = Em_st_data.list5;
                List<Comand_COM> comm_6_list = Em_st_data.list6;
                List<Comand_COM> comm_7_list = Em_st_data.list7;
                List<Comand_COM> comm_8_list = Em_st_data.list8;
                //ТОВО загрузить их или получить из аргументов функции

                //зпускаем тайемры + передаем настройки
                RunTimer(comm_1_list,
                         comm_2_list,
                         comm_3_list,
                         comm_4_list,
                         comm_5_list,
                         comm_6_list,
                         comm_7_list,
                         comm_8_list,
                         Em_st_data.timers_set,
                         Em_st_data.com_settings,
                         Em_st_data.max_min_target);

            }
            public override void SS_handle()
            {
                need_kill_all = true;
                Kill_all_timers();
                Log_mes?.Invoke("Em  - Go to SS");
                Log_.Debug("Em  - Go to SS");
                //_context._Data.st_SS.max_min_target.TAR_data_slice = _context._reserved.st_SS.max_min_target.TAR_data_slice;
                _context._Data.st_SS.max_min_target.TAR_data_slice = _context.Got_target_list_default("SS", _context.settings_path);
                _context.TransitionTo(new SS_state(new Log_Handler(Log_mes), _context._Data.st_SS));
                GC.Collect();
            }
            public override void Er_handle()
            {
                Log_mes?.Invoke("Em - Er is forbidden - do nothing");
                Log_.Debug("Em - Er is forbidden - do nothing");
            }
            public override void PLLL_handle()
            {
                Log_mes?.Invoke("Em - PLLL is forbidden - do nothing");
                Log_.Debug("Em - PLLL is forbidden - do nothing");
            }
            public override void Em_handle()
            {
                Log_mes?.Invoke("Em - do nothing");
                Log_.Debug("Em - do nothing");
            }
            public override void PHLL_handle()
            {
                Log_mes?.Invoke("Em - PHLL is forbidden - do nothing");
                Log_.Debug("Em - PHLL is forbidden - do nothing");
            }
            public override void ALL_handle()
            {
                Log_.Debug(this.name + " - Open Load Lock fobidden - do nothing");
            }
            public override void AN_handle()
            {
                Log_.Debug(this.name + " - Analysis fobidden - do nothing");
            }
            public override void HVSB_handle()
            {
                Log_.Debug(this.name + " - High vaccum Stand By fobidden - do nothing");
            }
        }
        public class PLLL_state : State
        {
            private event Log_Handler Log_mes;
            public PLLL_state(Log_Handler Logger, State_data s_data)
            {
                State_data PLLL_st_data = s_data;
                //if (_context != null)
                //PLLL_st_data = _context._Data.st_PLLL;
                this.Log_message += Logger;
                Log_mes = Logger;
                this.name = "PLLL";
                Log_mes?.Invoke("State " + this.name + " was created");
                Log_.Debug("State " + this.name + " was created");
                this.MAXMIN_version = PLLL_st_data.MAX_MIN_version;
                //this.IP_adress = State_Machine._Data.IP_adress_port;
                need_kill_all = false;
                //грузим списки комманд для разных СОМов
                List<Comand_COM> comm_1_list = PLLL_st_data.list1;
                List<Comand_COM> comm_2_list = PLLL_st_data.list2;
                List<Comand_COM> comm_3_list = PLLL_st_data.list3;
                List<Comand_COM> comm_4_list = PLLL_st_data.list4;
                List<Comand_COM> comm_5_list = PLLL_st_data.list5;
                List<Comand_COM> comm_6_list = PLLL_st_data.list6;
                List<Comand_COM> comm_7_list = PLLL_st_data.list7;
                List<Comand_COM> comm_8_list = PLLL_st_data.list8;
                //ТОВО загрузить их или получить из аргументов функции

                //зпускаем тайемры + передаем настройки
                RunTimer(comm_1_list,
                         comm_2_list,
                         comm_3_list,
                         comm_4_list,
                         comm_5_list,
                         comm_6_list,
                         comm_7_list,
                         comm_8_list,
                         PLLL_st_data.timers_set,
                         PLLL_st_data.com_settings,
                         PLLL_st_data.max_min_target);
                //Log_mes?.Invoke("State " + this.name + " was created");
                //Log_mes?.Invoke(this.name);
            }
            public override void SS_handle()
            {
                if (Check_target_reached())
                {
                    need_kill_all = true;
                    Kill_all_timers();
                    Log_mes?.Invoke("PLLL  - Go to SS");
                    Log_.Debug("PLLL  - Go to SS");
                    //Log_.Trace("old target slice");
                    //Log_.Trace(Str_get_from_dict(_context._Data.st_SS.max_min_target.TAR_data_slice));
                    //Log_.Trace("reserved target slice");
                    //Log_.Trace(Str_get_from_dict(_context._reserved.st_SS.max_min_target.TAR_data_slice));
                    //_context._Data.st_SS.max_min_target.TAR_data_slice = _context._reserved.st_SS.max_min_target.TAR_data_slice;
                    _context._Data.st_SS.max_min_target.TAR_data_slice = _context.Got_target_list_default("SS", _context.settings_path);
                    _context.TransitionTo(new SS_state(new Log_Handler(Log_mes), _context._Data.st_SS));
                    GC.Collect();
                }
                else
                {
                    Log_.Debug(this.name + " not all targets reached");
                }
            }
            public override void PHLL_handle()
            {
                if (Check_target_reached())
                {
                    need_kill_all = true;
                    Kill_all_timers();
                    Log_mes?.Invoke("PLLL  - Go to PHLL");
                    Log_.Debug("PLLL  - Go to PHLL");
                    _context._Data.st_PHLL.max_min_target.TAR_data_slice = _context.Got_target_list_default("PHLL", _context.settings_path);
                    //_context._Data.st_PHLL.max_min_target.TAR_data_slice = _context._reserved.st_PHLL.max_min_target.TAR_data_slice;
                    _context.TransitionTo(new PHLL_state(new Log_Handler(Log_mes), _context._Data.st_PHLL));
                    GC.Collect();
                }
                else
                {
                    Log_.Debug(this.name + " not all targets reached");
                }
            }
            public override void PLLL_handle()
            {
                Log_mes?.Invoke("PLLL - do nothing");
                Log_.Debug("PLLL - do nothing");
            }
            public override void Er_handle()
            {
                need_kill_all = true;
                Kill_all_timers();
                Log_mes?.Invoke("PLLL  - Go to Er");
                Log_.Debug("PLLL  - Go to Er");
                //_context._Data.st_Er.max_min_target.TAR_data_slice = _context._reserved.st_Er.max_min_target.TAR_data_slice;
                _context._Data.st_Er.max_min_target.TAR_data_slice = _context.Got_target_list_default("Er", _context.settings_path);
                _context.TransitionTo(new Er_state(new Log_Handler(Log_mes), _context._Data.st_Er));
                GC.Collect();
            }
            public override void Em_handle()
            {
                need_kill_all = true;
                Kill_all_timers();
                Log_mes?.Invoke("PLLL  - Go to Em");
                Log_.Debug("PLLL  - Go to Em");
                //_context._Data.st_Em.max_min_target.TAR_data_slice = _context._reserved.st_Em.max_min_target.TAR_data_slice;
                _context._Data.st_Em.max_min_target.TAR_data_slice = _context.Got_target_list_default("Em", _context.settings_path);
                _context.TransitionTo(new Em_state(new Log_Handler(Log_mes), _context._Data.st_Em));
                GC.Collect();
            }
            public override void ALL_handle()
            {
                Log_.Debug(this.name + " - Open Load Lock fobidden - do nothing");
            }
            public override void AN_handle()
            {
                Log_.Debug(this.name + " - Analysis fobidden - do nothing");
            }
            public override void HVSB_handle()
            {
                Log_.Debug(this.name + " - High vaccum Stand By fobidden - do nothing");
            }
        }
    public class PHLL_state : State
    {
        private event Log_Handler Log_mes;
        public PHLL_state(Log_Handler Logger, State_data s_data)
        {
            State_data PHLL_st_data = s_data;
            this.Log_message += Logger;
            Log_mes = Logger;
            this.name = "PHLL";
            Log_mes?.Invoke("State " + this.name + " was created");
            Log_.Debug("State " + this.name + " was created");
            this.MAXMIN_version = PHLL_st_data.MAX_MIN_version;        
            need_kill_all = false;
            //грузим списки комманд для разных СОМов
            List<Comand_COM> comm_1_list = PHLL_st_data.list1;
            List<Comand_COM> comm_2_list = PHLL_st_data.list2;
            List<Comand_COM> comm_3_list = PHLL_st_data.list3;
            List<Comand_COM> comm_4_list = PHLL_st_data.list4;
            List<Comand_COM> comm_5_list = PHLL_st_data.list5;
            List<Comand_COM> comm_6_list = PHLL_st_data.list6;
            List<Comand_COM> comm_7_list = PHLL_st_data.list7;
            List<Comand_COM> comm_8_list = PHLL_st_data.list8;
            //ТОВО загрузить их или получить из аргументов функции
            //зпускаем тайемры + передаем настройки
            RunTimer(comm_1_list,
                         comm_2_list,
                         comm_3_list,
                         comm_4_list,
                         comm_5_list,
                         comm_6_list,
                         comm_7_list,
                         comm_8_list,
                         PHLL_st_data.timers_set,
                         PHLL_st_data.com_settings,
                         PHLL_st_data.max_min_target);
        }
        public override void SS_handle()
        {
            Log_mes?.Invoke("PHLL - SS is forbidden - do nothing");
            Log_.Debug("PHLL - SS is forbidden - do nothing");
        }
        public override void PHLL_handle()
        {
            Log_mes?.Invoke("PHLL - do nothing");
            Log_.Debug("PHLL - do nothing");
        }
        public override void PLLL_handle()
        {
           Log_.Debug("PHLL - PLLL is forbidden - do nothing");
        }
        public override void Er_handle()
        {
            need_kill_all = true;
            Kill_all_timers();
            Log_mes?.Invoke("PHLL  - Go to Er");
            Log_.Debug("PHLL  - Go to Er");
                //_context._Data.st_Er.max_min_target.TAR_data_slice = _context._reserved.st_Er.max_min_target.TAR_data_slice;
            _context._Data.st_Er.max_min_target.TAR_data_slice = _context.Got_target_list_default("Er", _context.settings_path);
            _context.TransitionTo(new Er_state(new Log_Handler(Log_mes), _context._Data.st_Er));
            GC.Collect();
        }
            public override void Em_handle()
            {
                need_kill_all = true;
                Kill_all_timers();
                Log_mes?.Invoke("PHLL  - Go to Em");
                Log_.Debug("PHLL  - Go to Em");
                //_context._Data.st_Em.max_min_target.TAR_data_slice = _context._reserved.st_Em.max_min_target.TAR_data_slice;
                _context._Data.st_Em.max_min_target.TAR_data_slice = _context.Got_target_list_default("Em", _context.settings_path);
                _context.TransitionTo(new Em_state(new Log_Handler(Log_mes), _context._Data.st_Em));
                GC.Collect();
            }
            public override void ALL_handle()
            {
                Log_.Debug(this.name + " - Open Load Lock fobibben - do nothing");
            }
            public override void AN_handle()
            {
                Log_.Debug(this.name + " - Analysis fobidden - do nothing");
            }
            public override void HVSB_handle()
            {
            if (Check_target_reached())
            {
                need_kill_all = true;
                Kill_all_timers();
                Log_mes?.Invoke(this.name + "  - Go to HVSB");
                Log_.Debug(this.name + "  - Go to HVSB");
                //_context._Data.st_PLLL.max_min_target.TAR_data_slice = _context._reserved.st_PLLL.max_min_target.TAR_data_slice;
                _context._Data.st_HVSB.max_min_target.TAR_data_slice = _context.Got_target_list_default("HVSB", _context.settings_path);
                _context.TransitionTo(new HVSB_state(new Log_Handler(Log_mes), _context._Data.st_HVSB));
                GC.Collect();
            }
            else
            {
                Log_.Debug(this.name + " not all targets reached");
            }
        }
        }
        /// <summary>
        /// состяние для открытия загрузочной камеры
        /// </summary>
        public class ALL_state : State
        {
            private event Log_Handler Log_mes;
            public ALL_state(Log_Handler Logger, State_data s_data)
            {
                State_data AL_data = s_data;
                this.Log_message += Logger;
                Log_mes = Logger;
                this.name = "ALL";
                Log_mes?.Invoke("State " + this.name + " was created");
                Log_.Debug("State " + this.name + " was created");
                this.MAXMIN_version = AL_data.MAX_MIN_version;
                //this.IP_adress = State_Machine._Data.IP_adress_port;
                need_kill_all = false;
                //грузим списки комманд для разных СОМов
                List<Comand_COM> comm_1_list = AL_data.list1;
                List<Comand_COM> comm_2_list = AL_data.list2;
                List<Comand_COM> comm_3_list = AL_data.list3;
                List<Comand_COM> comm_4_list = AL_data.list4;
                List<Comand_COM> comm_5_list = AL_data.list5;
                List<Comand_COM> comm_6_list = AL_data.list6;
                List<Comand_COM> comm_7_list = AL_data.list7;
                List<Comand_COM> comm_8_list = AL_data.list8;
                //ТОВО загрузить их или получить из аргументов функции

                //зпускаем тайемры + передаем настройки
                RunTimer(comm_1_list,
                         comm_2_list,
                         comm_3_list,
                         comm_4_list,
                         comm_5_list,
                         comm_6_list,
                         comm_7_list,
                         comm_8_list,
                         AL_data.timers_set,
                         AL_data.com_settings,
                         AL_data.max_min_target);
                //Log_mes?.Invoke("State " + this.name + " was created");
                //Log_mes?.Invoke(this.name);
            }
            public override void SS_handle()
            {
                Log_mes?.Invoke(this.name + " - SS is forbidden - do nothing");
                Log_.Debug(this.name + " - SS is forbidden - do nothing");
            }
            public override void PHLL_handle()
            {
                Log_mes?.Invoke(this.name + " - Pump High Vacuum is forbbiden - do nothing");
                Log_.Debug(this.name + " - Pump High Vacuum is forbbiden - do nothing");
            }
            public override void PLLL_handle()
            {
            if (Check_target_reached())
            {
                need_kill_all = true;
                Kill_all_timers();
                Log_mes?.Invoke(this.name + "  - Go to PLLL");
                Log_.Debug(this.name + " - Go to PLLL");
                //_context._Data.st_PLLL.max_min_target.TAR_data_slice = _context._reserved.st_PLLL.max_min_target.TAR_data_slice;
                _context._Data.st_PLLL.max_min_target.TAR_data_slice = _context.Got_target_list_default("PLLL", _context.settings_path);
                _context.TransitionTo(new PLLL_state(new Log_Handler(Log_mes), _context._Data.st_PLLL));
                GC.Collect();
            }
            else
            {
                Log_.Debug(this.name + " not all targets reached");
            }
            }
            public override void Er_handle()
            {
                need_kill_all = true;
                Kill_all_timers();
                Log_mes?.Invoke(this.name + "  - Go to Er");
                Log_.Debug(this.name + "  - Go to Er");
                //_context._Data.st_Er.max_min_target.TAR_data_slice = _context._reserved.st_Er.max_min_target.TAR_data_slice;
                _context._Data.st_Er.max_min_target.TAR_data_slice = _context.Got_target_list_default("Er", _context.settings_path);
                _context.TransitionTo(new Er_state(new Log_Handler(Log_mes), _context._Data.st_Er));
                GC.Collect();
            }
            public override void Em_handle()
            {
                need_kill_all = true;
                Kill_all_timers();
                Log_mes?.Invoke(this.name + "  - Go to Em");
                Log_.Debug(this.name + "  - Go to Em");
                //_context._Data.st_Em.max_min_target.TAR_data_slice = _context._reserved.st_Em.max_min_target.TAR_data_slice;
                _context._Data.st_Em.max_min_target.TAR_data_slice = _context.Got_target_list_default("Em", _context.settings_path);
                _context.TransitionTo(new Em_state(new Log_Handler(Log_mes), _context._Data.st_Em));
                GC.Collect();
            }
            public override void ALL_handle()
            {
                Log_mes?.Invoke(this.name + " - do nothing");
                Log_.Debug(this.name + " - do nothing");
            }
        public override void AN_handle()
        {
            Log_.Debug(this.name + " - Analysis state is forbidden - do nothing");
            //TODO потом будет разрешено(типа грузить во время исследования)
        }
        public override void HVSB_handle()
        {
            Log_.Debug(this.name + " - High Vacuum StandBY is forbidden - do nothing");
        }

    }
    public class HVSB_state : State
    {
        private event Log_Handler Log_mes;
        public HVSB_state(Log_Handler Logger, State_data s_data)
        {
            State_data HVSB_st_data = s_data;
            this.Log_message += Logger;
            Log_mes = Logger;
            this.name = "HVSB";
            Log_mes?.Invoke("State " + this.name + " was created");
            Log_.Debug("State " + this.name + " was created");
            this.MAXMIN_version = HVSB_st_data.MAX_MIN_version;
            need_kill_all = false;
            //грузим списки комманд для разных СОМов
            List<Comand_COM> comm_1_list = HVSB_st_data.list1;
            List<Comand_COM> comm_2_list = HVSB_st_data.list2;
            List<Comand_COM> comm_3_list = HVSB_st_data.list3;
            List<Comand_COM> comm_4_list = HVSB_st_data.list4;
            List<Comand_COM> comm_5_list = HVSB_st_data.list5;
            List<Comand_COM> comm_6_list = HVSB_st_data.list6;
            List<Comand_COM> comm_7_list = HVSB_st_data.list7;
            List<Comand_COM> comm_8_list = HVSB_st_data.list8;
            //ТОВО загрузить их или получить из аргументов функции

            //зпускаем тайемры + передаем настройки
            RunTimer(comm_1_list,
                     comm_2_list,
                     comm_3_list,
                     comm_4_list,
                     comm_5_list,
                     comm_6_list,
                     comm_7_list,
                     comm_8_list,
                     HVSB_st_data.timers_set,
                     HVSB_st_data.com_settings,
                     HVSB_st_data.max_min_target);
        }
        public override void HVSB_handle()
        {
            Log_.Debug(this.name + " - do nothing");
        }
        public override void Em_handle()
        {
            need_kill_all = true;
            Kill_all_timers();
            Log_mes?.Invoke(this.name + "  - Go to Em");
            Log_.Debug(this.name + "  - Go to Em");
            //_context._Data.st_Em.max_min_target.TAR_data_slice = _context._reserved.st_Em.max_min_target.TAR_data_slice;
            _context._Data.st_Em.max_min_target.TAR_data_slice = _context.Got_target_list_default("Em", _context.settings_path);
            _context.TransitionTo(new Em_state(new Log_Handler(Log_mes), _context._Data.st_Em));
            GC.Collect();
        }
        public override void Er_handle()
        {
            need_kill_all = true;
            Kill_all_timers();
            Log_mes?.Invoke(this.name + "  - Go to Er");
            Log_.Debug(this.name + "  - Go to Er");
            //_context._Data.st_Er.max_min_target.TAR_data_slice = _context._reserved.st_Er.max_min_target.TAR_data_slice;
            _context._Data.st_Er.max_min_target.TAR_data_slice = _context.Got_target_list_default("Er", _context.settings_path);
            _context.TransitionTo(new Er_state(new Log_Handler(Log_mes), _context._Data.st_Er));
            GC.Collect();
        }
        public override void ALL_handle()
        {
            if (Check_target_reached())
            {
                need_kill_all = true;
                Kill_all_timers();
                Log_mes?.Invoke(this.name + "  - Go to ALL");
                Log_.Debug(this.name + "  - Go to ALL");
                //_context._Data.st_PLLL.max_min_target.TAR_data_slice = _context._reserved.st_PLLL.max_min_target.TAR_data_slice;
                _context._Data.st_ALL.max_min_target.TAR_data_slice = _context.Got_target_list_default("ALL", _context.settings_path);
                _context.TransitionTo(new ALL_state(new Log_Handler(Log_mes), _context._Data.st_ALL));
                GC.Collect();
            }
            else
            {
                Log_.Debug(this.name + " not all targets reached");
            }
        }
        public override void AN_handle()
        {
            if (Check_target_reached())
            {
                need_kill_all = true;
                Kill_all_timers();
                Log_mes?.Invoke(this.name + "  - Go to AN");
                Log_.Debug(this.name + "  - Go to AN");
                //_context._Data.st_PLLL.max_min_target.TAR_data_slice = _context._reserved.st_PLLL.max_min_target.TAR_data_slice;
                _context._Data.st_AN.max_min_target.TAR_data_slice = _context.Got_target_list_default("AN", _context.settings_path);
                _context.TransitionTo(new AN_state(new Log_Handler(Log_mes), _context._Data.st_AN));
                GC.Collect();
            }
            else
            {
                Log_.Debug(this.name + " not all targets reached");
            }
        }
        public override void SS_handle()
        {
            Log_.Debug(this.name + " - Safe State is forbidden - do nothing");
        }
        public override void PHLL_handle()
        {
            Log_.Debug(this.name + " - Pump High Vacuum is forbidden - do nothing");
        }
        public override void PLLL_handle()
        {
            Log_.Debug(this.name + " - Pump Low Vacuum is forbidden - do nothing");
        }

    }
    public class AN_state : State
    {
        private event Log_Handler Log_mes;
        public AN_state(Log_Handler Logger, State_data s_data)
        {
            State_data AN_data = s_data;
            this.Log_message += Logger;
            Log_mes = Logger;
            this.name = "AN";
            Log_mes?.Invoke("State " + this.name + " was created");
            Log_.Debug("State " + this.name + " was created");
            this.MAXMIN_version = AN_data.MAX_MIN_version;
            //this.IP_adress = State_Machine._Data.IP_adress_port;
            need_kill_all = false;
            //грузим списки комманд для разных СОМов
            List<Comand_COM> comm_1_list = AN_data.list1;
            List<Comand_COM> comm_2_list = AN_data.list2;
            List<Comand_COM> comm_3_list = AN_data.list3;
            List<Comand_COM> comm_4_list = AN_data.list4;
            List<Comand_COM> comm_5_list = AN_data.list5;
            List<Comand_COM> comm_6_list = AN_data.list6;
            List<Comand_COM> comm_7_list = AN_data.list7;
            List<Comand_COM> comm_8_list = AN_data.list8;
            //ТОВО загрузить их или получить из аргументов функции

            //зпускаем тайемры + передаем настройки
            RunTimer(comm_1_list,
                     comm_2_list,
                     comm_3_list,
                     comm_4_list,
                     comm_5_list,
                     comm_6_list,
                     comm_7_list,
                     comm_8_list,
                     AN_data.timers_set,
                     AN_data.com_settings,
                     AN_data.max_min_target);
            //Log_mes?.Invoke("State " + this.name + " was created");
            //Log_mes?.Invoke(this.name);
        }
        public override void SS_handle()
        {
            Log_mes?.Invoke(this.name + " - SS is forbidden - do nothing");
            Log_.Debug(this.name + " - SS is forbidden - do nothing");
        }
        public override void PHLL_handle()
        {
            Log_mes?.Invoke(this.name + " - Pump High Vacuum is forbbiden - do nothing");
            Log_.Debug(this.name + " - Pump High Vacuum is forbbiden - do nothing");
        }
        public override void PLLL_handle()
        {
            Log_mes?.Invoke(this.name + " - Pump Low Vacuum is forbbiden - do nothing");
            Log_.Debug(this.name + " - Pump Low Vacuum is forbbiden - do nothing");
        }
        public override void Er_handle()
        {
            need_kill_all = true;
            Kill_all_timers();
            Log_mes?.Invoke(this.name + "  - Go to Er");
            Log_.Debug(this.name + "  - Go to Er");
            //_context._Data.st_Er.max_min_target.TAR_data_slice = _context._reserved.st_Er.max_min_target.TAR_data_slice;
            _context._Data.st_Er.max_min_target.TAR_data_slice = _context.Got_target_list_default("Er", _context.settings_path);
            _context.TransitionTo(new Er_state(new Log_Handler(Log_mes), _context._Data.st_Er));
            GC.Collect();
        }
        public override void Em_handle()
        {
            need_kill_all = true;
            Kill_all_timers();
            Log_mes?.Invoke(this.name + "  - Go to Em");
            Log_.Debug(this.name + "  - Go to Em");
            //_context._Data.st_Em.max_min_target.TAR_data_slice = _context._reserved.st_Em.max_min_target.TAR_data_slice;
            _context._Data.st_Em.max_min_target.TAR_data_slice = _context.Got_target_list_default("Em", _context.settings_path);
            _context.TransitionTo(new Em_state(new Log_Handler(Log_mes), _context._Data.st_Em));
            GC.Collect();
        }
        public override void ALL_handle()
        {
            Log_mes?.Invoke(this.name + " Open Load Lock is fobidden - do nothing");
            Log_.Debug(this.name + " Open Load Lock is fobidden - do nothing");
        }
        public override void AN_handle()
        {
            Log_.Debug(this.name + " - do nothing");            
        }
        public override void HVSB_handle()
        {
            if (Check_target_reached())
            {
                need_kill_all = true;
                Kill_all_timers();
                Log_mes?.Invoke(this.name + "  - Go to HVSB");
                Log_.Debug(this.name + " - Go to HVSB");
                //_context._Data.st_PLLL.max_min_target.TAR_data_slice = _context._reserved.st_PLLL.max_min_target.TAR_data_slice;
                _context._Data.st_HVSB.max_min_target.TAR_data_slice = _context.Got_target_list_default("HVSB", _context.settings_path);
                _context.TransitionTo(new HVSB_state(new Log_Handler(Log_mes), _context._Data.st_HVSB));
                GC.Collect();
            }
            else
            {
                Log_.Debug(this.name + " not all targets reached");
            }
        }

    }
}
