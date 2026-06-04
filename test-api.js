const http = require('https');

const data = JSON.stringify({
  terminalId: "00000000-0000-0000-0000-000000000001",
  cashierId: "00000000-0000-0000-0000-000000000001",
  openingFloatCash: 500
});

const options = {
  hostname: 'ai-powered-pos-and-erp-system-h21v.onrender.com',
  port: 443,
  path: '/api/pos/session/open',
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Content-Length': data.length
  }
};

const req = http.request(options, (res) => {
  let body = '';
  res.on('data', d => body += d);
  res.on('end', () => {
    console.log("OPEN STATUS:", res.statusCode);
    console.log("OPEN BODY:", body);
    
    if (res.statusCode === 200) {
      const sessionId = body.replace(/"/g, '');
      console.log("Got sessionId:", sessionId);
      
      const closeData = JSON.stringify({
        sessionId: sessionId,
        actualClosingCash: 500
      });
      
      const closeOptions = {
        hostname: 'ai-powered-pos-and-erp-system-h21v.onrender.com',
        port: 443,
        path: '/api/pos/session/close',
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Content-Length': closeData.length
        }
      };
      
      const closeReq = http.request(closeOptions, (cRes) => {
        let cBody = '';
        cRes.on('data', d => cBody += d);
        cRes.on('end', () => {
          console.log("CLOSE STATUS:", cRes.statusCode);
          console.log("CLOSE BODY:", cBody);
        });
      });
      
      closeReq.write(closeData);
      closeReq.end();
    }
  });
});

req.write(data);
req.end();
