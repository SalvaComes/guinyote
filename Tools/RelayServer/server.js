// Relay minimo para Guiñote: cada URL (/CODIGO) es una sala y cada mensaje se reenvia
// a los demas clientes de esa sala. No guarda nada ni interpreta los mensajes.
//
//   npm install ws
//   node server.js            (puerto: variable PORT, 8080 por defecto)
//
// Se puede alojar gratis en Render, Fly.io, Railway, etc. (usan wss:// por ti).

const { WebSocketServer } = require('ws');

const wss = new WebSocketServer({ port: process.env.PORT || 8080 });
const salas = new Map(); // codigo -> Set<WebSocket>

wss.on('connection', (ws, req) => {
  const sala = (req.url || '/').split('?')[0].replace(/^\/+/, '') || 'general';
  if (!salas.has(sala)) salas.set(sala, new Set());
  const miembros = salas.get(sala);

  if (miembros.size >= 4) { ws.close(1013, 'sala llena'); return; }
  miembros.add(ws);

  ws.on('message', (datos, esBinario) => {
    for (const otro of miembros)
      if (otro !== ws && otro.readyState === 1) otro.send(datos, { binary: esBinario });
  });

  ws.on('close', () => {
    miembros.delete(ws);
    if (miembros.size === 0) salas.delete(sala);
  });
});

console.log(`Relay escuchando en el puerto ${wss.options.port}`);
