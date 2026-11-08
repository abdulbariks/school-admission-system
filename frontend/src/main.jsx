import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { GraduationCap, LayoutDashboard, Users, BookOpen, Clock3, CheckCircle2, AlertCircle, Plus, RefreshCw, ArrowUpRight, ShieldCheck, Search, X, CreditCard, LogOut, Menu, School, Sparkles } from 'lucide-react';
import './style.css';

const API = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080').replace(/\/$/, '');
const studentKey = 'school-admission-student-id';
const api = async (path, options = {}, studentId) => {
  const response = await fetch(`${API}${path}`, { ...options, headers: { 'Content-Type': 'application/json', ...(studentId ? { 'X-Student-Id': studentId } : {}), ...options.headers } });
  if (response.status === 204) return null;
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(data.detail || data.title || `Request failed (${response.status})`);
  return data;
};
const fmt = (value) => value ? new Date(value).toLocaleString() : '—';

function App() {
  const [view, setView] = useState('parent');
  const [studentId, setStudentId] = useState(localStorage.getItem(studentKey) || '');
  const [classes, setClasses] = useState([]);
  const [holds, setHolds] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [selected, setSelected] = useState(null);
  const [name, setName] = useState('');
  const [seats, setSeats] = useState('40');
  const [creating, setCreating] = useState(false);

  const refresh = useCallback(async () => {
    setLoading(true); setError('');
    try {
      const [c, h] = await Promise.all([api('/classes'), studentId.length === 17 ? api('/me/holds', {}, studentId) : Promise.resolve([])]);
      setClasses(c); setHolds(h);
    } catch (e) { setError(e.message); }
    finally { setLoading(false); }
  }, [studentId]);
  useEffect(() => { refresh(); const timer = setInterval(refresh, 10000); return () => clearInterval(timer); }, [refresh]);
  useEffect(() => { if (notice) { const t = setTimeout(() => setNotice(''), 4000); return () => clearTimeout(t); } }, [notice]);
  const activeHolds = holds.filter(h => h.status === 'HELD');
  const confirmed = holds.filter(h => h.status === 'CONFIRMED');

  const saveStudent = (value) => {
    setStudentId(value); localStorage.setItem(studentKey, value);
  };
  const apply = async (item) => {
    if (!/^\d{17}$/.test(studentId)) { setError('Please enter a valid 17-digit birth registration number first.'); return; }
    setError('');
    try { const result = await api(`/classes/${item.id}/holds`, { method: 'POST' }, studentId); setSelected(result); setNotice('Seat reserved successfully. Complete payment before it expires.'); await refresh(); }
    catch (e) { setError(e.message); }
  };
  const confirm = async (hold) => {
    try { await api(`/holds/${hold.holdId}/confirm`, { method: 'POST' }, studentId); setNotice('Payment successful. Admission confirmed!'); await refresh(); }
    catch (e) { setError(e.message); await refresh(); }
  };
  const cancel = async (hold) => {
    try { await api(`/holds/${hold.holdId}`, { method: 'DELETE' }, studentId); setNotice('Application cancelled. The seat is available again.'); await refresh(); }
    catch (e) { setError(e.message); }
  };
  const createClass = async (e) => {
    e.preventDefault(); setCreating(true);
    try { await api('/classes', { method: 'POST', body: JSON.stringify({ name, totalSeats: Number(seats) }) }); setName(''); setSeats('40'); setNotice('Class and seat capacity created.'); await refresh(); }
    catch (e) { setError(e.message); }
    finally { setCreating(false); }
  };

  return <div className="app-shell">
    <aside className="sidebar">
      <div className="brand"><div className="brand-icon"><GraduationCap size={23}/></div><div><strong>Admitly</strong><span>School portal</span></div></div>
      <div className="school-chip"><div className="school-avatar"><School size={19}/></div><div><b>Greenfield School</b><small>Admission 2026–27</small></div></div>
      <div className="nav-label">WORKSPACE</div>
      <button className={`nav-item ${view === 'parent' ? 'active' : ''}`} onClick={() => setView('parent')}><LayoutDashboard size={18}/> Parent portal</button>
      <button className={`nav-item ${view === 'admin' ? 'active' : ''}`} onClick={() => setView('admin')}><ShieldCheck size={18}/> School admin</button>
      <div className="sidebar-bottom"><div className="help-card"><div className="help-icon"><Sparkles size={17}/></div><b>Need help?</b><p>Choose a class, reserve a seat, and confirm your admission.</p></div><div className="profile"><div className="profile-avatar">{view === 'admin' ? 'SA' : 'PG'}</div><div><b>{view === 'admin' ? 'School Admin' : 'Parent / Guardian'}</b><small>{view === 'admin' ? 'Administrator' : 'Admission applicant'}</small></div><LogOut size={16} className="muted-icon"/></div></div>
    </aside>
    <main className="main-area">
      <header className="topbar"><div className="crumb"><span>Workspace</span><b>/</b><strong>{view === 'admin' ? 'School administration' : 'Parent dashboard'}</strong></div><div className="top-actions"><span className="live-dot">● System operational</span><button className="icon-btn" onClick={refresh} title="Refresh"><RefreshCw size={17}/></button><div className="top-avatar">{view === 'admin' ? 'SA' : 'PG'}</div></div></header>
      <div className="content">
        {error && <div className="alert error"><AlertCircle size={18}/><span>{error}</span><button onClick={() => setError('')}><X size={16}/></button></div>}
        {notice && <div className="alert success"><CheckCircle2 size={18}/><span>{notice}</span><button onClick={() => setNotice('')}><X size={16}/></button></div>}
        {view === 'parent' ? <>
          <section className="welcome-row"><div><div className="eyebrow">ADMISSIONS OPEN · 2026–27</div><h1>Find your child's <em>next chapter.</em></h1><p>Explore available classes and secure a seat through our simple online admission process.</p></div><div className="welcome-art"><div className="art-circle"><GraduationCap size={48}/></div><span className="art-star star-one">✦</span><span className="art-star star-two">✧</span></div></section>
          <section className="student-panel"><div className="student-icon"><Users size={21}/></div><div className="student-copy"><b>Student identification</b><span>Enter the 17-digit birth registration number to manage applications.</span></div><div className="student-input-wrap"><input value={studentId} maxLength={17} inputMode="numeric" placeholder="17-digit birth registration number" onChange={e => saveStudent(e.target.value.replace(/\D/g, ''))}/><span className={studentId.length === 17 ? 'valid' : 'length'}>{studentId.length}/17</span></div></section>
          <div className="section-heading"><div><div className="eyebrow">YOUR ADMISSION JOURNEY</div><h2>Overview</h2></div><button className="text-button" onClick={refresh}><RefreshCw size={15}/> Refresh data</button></div>
          <div className="stats-grid"><Stat icon={<BookOpen/>} label="Available programs" value={classes.length} tint="lavender"/><Stat icon={<Clock3/>} label="Active reservations" value={activeHolds.length} tint="amber"/><Stat icon={<CheckCircle2/>} label="Confirmed admissions" value={confirmed.length} tint="mint"/></div>
          <div className="section-heading class-heading"><div><div className="eyebrow">CHOOSE YOUR PREFERRED CLASS</div><h2>Available classes <span className="count-pill">{classes.length}</span></h2></div><span className="first-come"><span/> First come, first served</span></div>
          {loading && !classes.length ? <div className="empty-state">Loading classes…</div> : classes.length ? <div className="class-grid">{classes.map(item => <article className="class-card" key={item.id}><div className="class-card-top"><div className="class-symbol"><BookOpen size={20}/></div><span className={item.availableSeats > 0 ? 'status-open' : 'status-full'}>{item.availableSeats > 0 ? '● Seats available' : '● Fully booked'}</span></div><h3>{item.name}</h3><div className="seat-number"><strong>{item.availableSeats}</strong><span>seats remaining</span></div><div className="seat-track"><div style={{width: `${Math.min(100, (item.confirmedSeats + item.heldSeats) / item.totalSeats * 100)}%`}}/></div><div className="seat-breakdown"><span>Total <b>{item.totalSeats}</b></span><span>Reserved <b>{item.heldSeats}</b></span><span>Confirmed <b>{item.confirmedSeats}</b></span></div><button className="primary-btn full-btn" disabled={item.availableSeats < 1} onClick={() => apply(item)}>Apply for admission <ArrowUpRight size={17}/></button></article>)}</div> : <div className="empty-state"><BookOpen size={30}/><b>No classes published yet</b><span>School administration will add classes here soon.</span></div>}
          <div className="section-heading application-heading"><div><div className="eyebrow">TRACK YOUR PROGRESS</div><h2>My applications</h2></div><span className="muted-note">Automatically refreshed every 10 seconds</span></div>
          {holds.length ? <div className="table-card"><div className="table-scroll"><table><thead><tr><th>CLASS</th><th>APPLICATION ID</th><th>STATUS</th><th>RESERVATION EXPIRES</th><th>ACTION</th></tr></thead><tbody>{holds.map(h => <tr key={h.holdId}><td><b>{h.className}</b></td><td className="mono">{h.holdId.slice(0, 13)}…</td><td><Status value={h.status}/></td><td>{h.status === 'HELD' ? fmt(h.expiresAt) : '—'}</td><td>{h.status === 'HELD' ? <div className="row-actions"><button className="pay-btn" onClick={() => confirm(h)}><CreditCard size={14}/> Pay fee</button><button className="cancel-btn" onClick={() => cancel(h)}>Cancel</button></div> : '—'}</td></tr>)}</tbody></table></div></div> : <div className="empty-inline">Your applications will appear here after you reserve a seat.</div>}
        </> : <>
          <section className="admin-welcome"><div><div className="eyebrow">CONTROL CENTER</div><h1>School <em>administration.</em></h1><p>Create admission programs and monitor seat availability in real time.</p></div><div className="admin-badge"><ShieldCheck size={38}/><span>ADMIN ACCESS</span></div></section>
          <div className="section-heading"><div><div className="eyebrow">PROGRAM MANAGEMENT</div><h2>Create a class or shift</h2></div></div>
          <form className="create-panel" onSubmit={createClass}><div className="form-intro"><div className="form-icon"><Plus size={21}/></div><div><b>New admission program</b><span>Each class and shift has its own seat capacity.</span></div></div><label>Class / shift name<input required maxLength={120} value={name} onChange={e => setName(e.target.value)} placeholder="e.g. Class 6 - Morning Shift"/></label><label>Total seats<input required type="number" min="1" max="100000" value={seats} onChange={e => setSeats(e.target.value)}/></label><button className="primary-btn" disabled={creating}>{creating ? 'Creating…' : 'Create class'} <Plus size={17}/></button></form>
          <div className="section-heading admin-list-heading"><div><div className="eyebrow">LIVE CAPACITY MONITOR</div><h2>All classes <span className="count-pill">{classes.length}</span></h2></div><button className="text-button" onClick={refresh}><RefreshCw size={15}/> Refresh</button></div>
          <div className="table-card"><div className="table-scroll"><table><thead><tr><th>CLASS / SHIFT</th><th>TOTAL SEATS</th><th>HELD</th><th>CONFIRMED</th><th>AVAILABLE</th><th>UTILIZATION</th></tr></thead><tbody>{classes.map(c => <tr key={c.id}><td><b>{c.name}</b></td><td>{c.totalSeats}</td><td><span className="number-held">{c.heldSeats}</span></td><td><span className="number-confirmed">{c.confirmedSeats}</span></td><td><b>{c.availableSeats}</b></td><td><div className="mini-track"><span style={{width:`${(c.totalSeats-c.availableSeats)/c.totalSeats*100}%`}}/></div></td></tr>)}</tbody></table></div>{!classes.length && <div className="empty-inline">No classes yet. Create your first admission program above.</div>}</div>
          <div className="admin-note"><ShieldCheck size={18}/><div><b>Seat protection is enabled</b><span>Reservations are allocated transactionally. Concurrent applications cannot exceed the configured seat capacity.</span></div></div>
        </>}
        <footer><span>© 2026 Admitly · School Admission Portal</span><span>Secure admission management</span></footer>
      </div>
    </main>
  </div>;
}
function Stat({icon,label,value,tint}) { return <div className="stat-card"><div className={`stat-icon ${tint}`}>{React.cloneElement(icon,{size:19})}</div><div><span>{label}</span><strong>{value}</strong></div></div>; }
function Status({value}) { const cls = value.toLowerCase(); return <span className={`status-pill ${cls}`}>{value}</span>; }

createRoot(document.getElementById('root')).render(<React.StrictMode><App/></React.StrictMode>);
