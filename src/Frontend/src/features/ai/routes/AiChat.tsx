import React, { useState, useRef, useEffect } from 'react';
import { Sparkles, Send, Loader2, Bot, User, HelpCircle, BarChart3, PieChart as PieIcon, ClipboardList } from 'lucide-react';
import { sendChatMessage, getAiStatus, ChatMessageResponse } from '../api/ai.api';
import { ResponsiveContainer, BarChart, Bar, XAxis, YAxis, Tooltip, PieChart, Pie, Cell } from 'recharts';

interface Message {
  id: string;
  sender: 'user' | 'assistant';
  text: string;
  chartType?: 'BAR' | 'PIE' | 'LINE' | 'TABLE';
  chartData?: any[];
  timestamp: Date;
}

const COLORS = ['#6366f1', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899'];

export const AiChat = () => {
  const [messages, setMessages] = useState<Message[]>([
    {
      id: 'welcome',
      sender: 'assistant',
      text: "Hello! I am your AI ERP Co-pilot. I can query real-time analytics, sales figures, payment types, and inventory statuses from the database. Ask me a question, or use the quick links below!",
      timestamp: new Date()
    }
  ]);
  const [inputValue, setInputValue] = useState('');
  const [loading, setLoading] = useState(false);
  const [ollamaOnline, setOllamaOnline] = useState<boolean | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const checkOllamaStatus = async () => {
    try {
      const res = await getAiStatus();
      setOllamaOnline(res.ollamaOnline);
    } catch {
      setOllamaOnline(false);
    }
  };

  useEffect(() => {
    checkOllamaStatus();
  }, []);


  const promptCategories = [
    {
      title: '📈 Sales & Financials',
      prompts: [
        { label: 'Today\'s Sales', prompt: 'Today\'s sales summary' },
        { label: 'Yesterday\'s Sales', prompt: 'Yesterday\'s sales figure please' },
        { label: 'This Month', prompt: 'This month sales summary' },
        { label: 'Last Week', prompt: 'Show me last week\'s sales' },
        { label: 'Total Revenue', prompt: 'Total revenue and sales figure' },
      ]
    },
    {
      title: '📦 Inventory & Alerts',
      prompts: [
        { label: 'Top Sellers', prompt: 'Show my top selling products' },
        { label: 'Cash vs UPI Split', prompt: 'Show sales breakdown by payment type' },
        { label: 'Near-Expiry Alerts', prompt: 'Show near expiry batch alerts' },
        { label: 'Low Stock Levels', prompt: 'List low stock items' }
      ]
    }
  ];

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages, loading]);

  const handleSendMessage = async (textToSend: string) => {
    if (!textToSend.trim()) return;

    const userMessage: Message = {
      id: Date.now().toString(),
      sender: 'user',
      text: textToSend,
      timestamp: new Date()
    };

    setMessages(prev => [...prev, userMessage]);
    setInputValue('');
    setLoading(true);

    try {
      const response = await sendChatMessage(textToSend);
      
      const assistantMessage: Message = {
        id: (Date.now() + 1).toString(),
        sender: 'assistant',
        text: response.text,
        chartType: response.chartType,
        chartData: response.chartData,
        timestamp: new Date()
      };

      setMessages(prev => [...prev, assistantMessage]);
    } catch (error: any) {
      console.error('Chat error', error);
      const errorMessage: Message = {
        id: (Date.now() + 1).toString(),
        sender: 'assistant',
        text: 'Sorry, I encountered an error querying the system. Please try again.',
        timestamp: new Date()
      };
      setMessages(prev => [...prev, errorMessage]);
    } finally {
      setLoading(false);
    }
  };

  const renderChart = (chartType: string, chartData: any[]) => {
    if (!chartData || chartData.length === 0) return null;

    if (chartType === 'BAR') {
      return (
        <div className="w-full h-64 bg-slate-950/40 p-4 rounded-xl border border-slate-800/60 mt-3">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={chartData} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
              <defs>
                <linearGradient id="barGradient" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#6366f1" stopOpacity={1} />
                  <stop offset="100%" stopColor="#312e81" stopOpacity={0.3} />
                </linearGradient>
              </defs>
              <XAxis dataKey="name" stroke="#94a3b8" fontSize={10} tickLine={false} />
              <YAxis stroke="#94a3b8" fontSize={10} tickLine={false} />
              <Tooltip 
                contentStyle={{ backgroundColor: '#0f172a', borderColor: '#1e293b', borderRadius: '8px' }}
                labelStyle={{ color: '#fff', fontWeight: 'bold' }}
                itemStyle={{ color: '#818cf8' }}
              />
              <Bar dataKey="value" fill="url(#barGradient)" radius={[6, 6, 0, 0]} isAnimationActive={true} animationDuration={1200} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      );
    }

    if (chartType === 'PIE') {
      return (
        <div className="w-full h-64 bg-slate-950/40 p-4 rounded-xl border border-slate-800/60 mt-3 flex items-center justify-center">
          <ResponsiveContainer width="100%" height="100%">
            <PieChart>
              <Pie
                data={chartData}
                cx="50%"
                cy="50%"
                innerRadius={60}
                outerRadius={85}
                paddingAngle={5}
                dataKey="value"
                isAnimationActive={true}
                animationDuration={1000}
              >
                {chartData.map((_, index) => (
                  <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                ))}
              </Pie>
              <Tooltip 
                contentStyle={{ backgroundColor: '#0f172a', borderColor: '#1e293b', borderRadius: '8px' }}
                itemStyle={{ color: '#fff' }}
              />
            </PieChart>
          </ResponsiveContainer>
          <div className="text-left text-xs font-semibold space-y-1.5 ml-4 shrink-0">
            {chartData.map((entry, idx) => (
              <div key={idx} className="flex items-center gap-1.5 text-slate-300">
                <span className="w-3 h-3 rounded-full" style={{ backgroundColor: COLORS[idx % COLORS.length] }}></span>
                <span>{entry.name}: ₹{entry.value.toFixed(2)}</span>
              </div>
            ))}
          </div>
        </div>
      );
    }

    return null;
  };

  return (
    <div className="max-w-4xl mx-auto p-6 h-[calc(100vh-4rem)] flex flex-col justify-between">
      <style>{`
        .glass-panel {
          background: rgba(15, 23, 42, 0.45);
          backdrop-filter: blur(16px);
          border: 1px solid rgba(255, 255, 255, 0.05);
        }
        .chat-scrollbar::-webkit-scrollbar {
          width: 6px;
        }
        .chat-scrollbar::-webkit-scrollbar-track {
          background: transparent;
        }
        .chat-scrollbar::-webkit-scrollbar-thumb {
          background: rgba(148, 163, 184, 0.15);
          border-radius: 9999px;
        }
        .chat-scrollbar::-webkit-scrollbar-thumb:hover {
          background: rgba(148, 163, 184, 0.3);
        }
        .user-bubble {
          background: linear-gradient(135deg, #4f46e5 0%, #3b82f6 100%);
          box-shadow: 0 4px 15px -3px rgba(79, 70, 229, 0.35);
        }
        .assistant-bubble {
          background: rgba(30, 41, 59, 0.6);
          backdrop-filter: blur(8px);
          border: 1px solid rgba(255, 255, 255, 0.04);
          box-shadow: 0 4px 20px -3px rgba(0, 0, 0, 0.3);
        }
        .quick-chip {
          background: rgba(30, 41, 59, 0.4);
          border: 1px solid rgba(255, 255, 255, 0.05);
          transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
        }
        .quick-chip:hover {
          background: rgba(79, 70, 229, 0.2);
          border-color: rgba(99, 102, 241, 0.4);
          transform: translateY(-1px);
        }
      `}</style>

      {/* Header */}
      <div className="glass-panel p-4 rounded-t-xl flex justify-between items-center shadow-2xl relative z-10">
        <div className="flex items-center space-x-3">
          <div className="w-10 h-10 bg-indigo-600 rounded-lg flex items-center justify-center shadow-md">
            <Sparkles className="w-5 h-5 text-white" />
          </div>
          <div>
            <h2 className="text-md font-extrabold text-white">AI Co-pilot Dashboard</h2>
            <p className="text-xs text-indigo-400 font-semibold flex items-center gap-1">
              <Bot className="w-3 h-3 text-emerald-400" /> Live Database Integration Active
            </p>
          </div>
        </div>

        {/* Ollama Status Badge */}
        <div className="flex items-center gap-2">
          {ollamaOnline === null ? (
            <span className="px-3 py-1 bg-slate-950/60 border border-slate-800 text-[10px] font-bold text-slate-400 rounded-full flex items-center gap-1.5">
              <span className="w-1.5 h-1.5 rounded-full bg-slate-500 animate-pulse"></span>
              Checking LLM...
            </span>
          ) : ollamaOnline ? (
            <span className="px-3 py-1 bg-emerald-950/40 border border-emerald-500/20 text-[10px] font-bold text-emerald-400 rounded-full flex items-center gap-1.5 shadow-sm shadow-emerald-500/5">
              <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
              Ollama LLM: Online
            </span>
          ) : (
            <span 
              onClick={checkOllamaStatus}
              title="Click to check status again"
              className="px-3 py-1 bg-amber-950/40 border border-amber-500/20 text-[10px] font-bold text-amber-450 rounded-full flex items-center gap-1.5 cursor-pointer hover:bg-amber-950/60 transition shadow-sm shadow-amber-500/5"
            >
              <span className="w-1.5 h-1.5 rounded-full bg-amber-500"></span>
              Ollama LLM: Offline (Database Fallback Active)
            </span>
          )}
        </div>
      </div>

      {/* Message Screen */}
      <div className="flex-1 bg-slate-950/60 backdrop-blur-sm border-x border-slate-900 p-6 overflow-y-auto space-y-6 chat-scrollbar">
        {messages.map((msg) => (
          <div
            key={msg.id}
            className={`flex ${msg.sender === 'user' ? 'justify-end' : 'justify-start'} animate-in fade-in slide-in-from-bottom-2 duration-200`}
          >
            <div className={`flex gap-3 max-w-[80%] ${msg.sender === 'user' ? 'flex-row-reverse' : 'flex-row'}`}>
              {/* Avatar */}
              <div className={`w-8 h-8 rounded-full flex items-center justify-center shrink-0 shadow ${
                msg.sender === 'user' 
                  ? 'bg-blue-600 text-white' 
                  : 'bg-indigo-600/20 text-indigo-400 border border-indigo-500/20'
              }`}>
                {msg.sender === 'user' ? <User className="w-4 h-4" /> : <Bot className="w-4 h-4" />}
              </div>

              {/* Bubble */}
              <div className={`p-4 rounded-2xl text-sm ${
                msg.sender === 'user'
                  ? 'user-bubble text-white rounded-tr-none'
                  : 'assistant-bubble text-slate-100 rounded-tl-none'
              }`}>
                {/* Text */}
                <div className="whitespace-pre-line leading-relaxed font-medium">
                  {msg.text}
                </div>

                {/* Inline Chart */}
                {msg.chartType && msg.chartData && renderChart(msg.chartType, msg.chartData)}

                {/* Timestamp */}
                <div className={`text-[10px] mt-2 block text-right font-medium ${
                  msg.sender === 'user' ? 'text-blue-200' : 'text-slate-500'
                }`}>
                  {msg.timestamp.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                </div>
              </div>
            </div>
          </div>
        ))}

        {loading && (
          <div className="flex justify-start">
            <div className="flex gap-3 items-center">
              <div className="w-8 h-8 rounded-full bg-indigo-600/20 text-indigo-400 border border-indigo-500/20 flex items-center justify-center shrink-0">
                <Bot className="w-4 h-4" />
              </div>
              <div className="assistant-bubble px-4 py-3 rounded-2xl rounded-tl-none flex items-center space-x-2 text-slate-350 text-xs font-bold">
                <Loader2 className="w-4 h-4 animate-spin text-indigo-500" />
                <span>AI is compiling database query...</span>
              </div>
            </div>
          </div>
        )}
        <div ref={messagesEndRef} />
      </div>

      {/* Input / Control Area */}
      <div className="glass-panel p-4 rounded-b-xl space-y-4 shadow-2xl relative z-10">
        {/* Quick Chips Categorized */}
        <div className="space-y-3">
          {promptCategories.map((cat, catIdx) => (
            <div key={catIdx} className="flex flex-col space-y-1">
              <span className="text-[10px] font-black text-slate-500 uppercase tracking-wider">{cat.title}</span>
              <div className="flex gap-2 overflow-x-auto pb-1 scrollbar-none">
                {cat.prompts.map((chip, idx) => (
                  <button
                    key={idx}
                    type="button"
                    onClick={() => handleSendMessage(chip.prompt)}
                    className="quick-chip px-3.5 py-1.5 text-slate-300 hover:text-indigo-300 rounded-full text-xs font-bold transition flex items-center shrink-0 gap-1.5"
                  >
                    {chip.label}
                  </button>
                ))}
              </div>
            </div>
          ))}
        </div>

        {/* Input box */}
        <form
          onSubmit={(e) => {
            e.preventDefault();
            handleSendMessage(inputValue);
          }}
          className="flex gap-2 pt-1"
        >
          <input
            type="text"
            className="flex-1 px-4 py-3 bg-slate-950/70 border border-slate-800/80 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 rounded-xl text-white placeholder-slate-500 focus:outline-none text-sm transition"
            placeholder="Type a query (e.g. 'Show top selling products' or 'Payment sales breakdown')..."
            value={inputValue}
            onChange={(e) => setInputValue(e.target.value)}
            disabled={loading}
          />
          <button
            type="submit"
            disabled={!inputValue.trim() || loading}
            className="px-5 py-3 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl transition flex items-center justify-center disabled:opacity-40 disabled:hover:bg-indigo-600 shadow-md shadow-indigo-600/10"
          >
            <Send className="w-4.5 h-4.5" />
          </button>
        </form>
      </div>
    </div>
  );
};
