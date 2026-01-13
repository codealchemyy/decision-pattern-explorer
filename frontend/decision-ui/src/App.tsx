/* import { useState } from 'react'
import reactLogo from './assets/react.svg'
import viteLogo from '/vite.svg'
import './App.css'

function App() {
  const [count, setCount] = useState(0)

  return (
    <>
      <div>
        <a href="https://vite.dev" target="_blank">
          <img src={viteLogo} className="logo" alt="Vite logo" />
        </a>
        <a href="https://react.dev" target="_blank">
          <img src={reactLogo} className="logo react" alt="React logo" />
        </a>
      </div>
      <h1>Vite + React</h1>
      <div className="card">
        <button onClick={() => setCount((count) => count + 1)}>
          count is {count}
        </button>
        <p>
          Edit <code>src/App.tsx</code> and save to test HMR
        </p>
      </div>
      <p className="read-the-docs">
        Click on the Vite and React logos to learn more
      </p>
    </>
  )
}

export default App
 */


import { useEffect, useState } from "react";
import "./App.css";

type Health = { status: string };

export default function App() {
  const [health, setHealth] = useState<Health | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const baseUrl = import.meta.env.VITE_API_BASE_URL as string;

    fetch(`${baseUrl}/health`)
      .then(async (res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return (await res.json()) as Health;
      })
      .then(setHealth)
      .catch((e) => setError(String(e.message ?? e)));
  }, []);

  return (
    <div style={{ padding: 24 }}>
      <h1>Decision Pattern Explorer</h1>

      <h2>Backend connection</h2>

      {error && <p style={{ color: "crimson" }}>Error: {error}</p>}

      {!error && !health && <p>Checking backend…</p>}

      {health && <p>Backend says: {health.status}</p>}
    </div>
  );
}
