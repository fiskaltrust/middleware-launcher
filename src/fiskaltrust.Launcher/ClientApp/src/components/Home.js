import React, { useState } from 'react';
import { useInterval } from '../useInterval';

function renderTable(monarchs) {
  return (
    <table className='table table-striped' aria-labelledby="tabelLabel">
      <thead>
        <tr>
          <th>State</th>
          <th>Guid</th>
          <th>Package</th>
          <th>Version</th>
        </tr>
      </thead>
      <tbody>
        {monarchs.map(monarch =>
          <tr key={monarch.packageConfiguration.id}>
            <td>{calculateState(monarch.hasStarted, monarch.hasStopped)}</td>
            <td>{monarch.packageConfiguration.id}</td>
            <td>{monarch.packageConfiguration.package}</td>
            <td>{monarch.packageConfiguration.version}</td>
          </tr>
        )}
      </tbody>
    </table>
  );
}

function calculateState(hasStarted, hasStopped) {
  if(!hasStopped) {
    if(!hasStarted) {
      return "Starting";
    } else {
      return "Started";
    }
  } else {
    if(hasStarted) {
      return "Stopped";
    } else {
      return "Crashed";
    }
  }
}

export function Home() {
  const [loading, setLoading] = useState(true);
  const [monarchs, setMonarchs] = useState([]);

  useInterval(() => {
    fetch('api/monarchs')
      .then(response => response.json())
      .then(data => {
        setMonarchs(data);
        setLoading(false);
      }).catch(() => {throw ""});
  }, 2500);

  let contents = loading
    ? <p><em>Loading...</em></p>
    : renderTable(monarchs);

  return (
    <div>
      <h1 id="tabelLabel" >Hosted Packages</h1>
      {contents}
    </div>
  );
}
