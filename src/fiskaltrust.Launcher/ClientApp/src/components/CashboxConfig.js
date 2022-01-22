import React, { useEffect, useState } from 'react';

export function CashboxConfig() {
  const [loading, setLoading] = useState(true);
  const [config, setConfig] = useState({});

  useEffect(() => {
    fetch('api/configuration/cashbox')
      .then(response => response.json())
      .then(data => {
        setConfig(data);
        setLoading(false);
      }).catch(() => {throw ""});
  }, []);

  let contents = loading
    ? <p><em>Loading...</em></p>
    : <pre>{JSON.stringify(config, null, 2)}</pre>;

  return (
    <div>
      <h1 id="tabelLabel" >Cashbox Configuration</h1>
      {contents}
    </div>
  );
}
