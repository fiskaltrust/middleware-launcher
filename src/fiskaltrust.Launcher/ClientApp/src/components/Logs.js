import React, { useEffect, useState } from 'react';
import { TabContent, TabPane, Nav, NavItem, NavLink } from 'reactstrap';
import classnames from 'classnames';
import { useInterval } from '../useInterval';

function Log() {
  const [loading, setLoading] = useState(true);
  const [logs, setLogs] = useState([]);

  useEffect(() => {
    fetch('api/logs')
      .then(response => response.json())
      .then(data => {
        setLogs(data);
        setLoading(false);
      }).catch(() => { throw "" });
  }, []);

  let contents = loading
    ? <p><em>Loading...</em></p>
    : logs;

  return (
    <div>
      <h1 id="tabelLabel" >Logs</h1>
      {contents}
    </div>
  );
}

export function Logs({ history, match: { params: { packageId } } }) {
  const [loading, setLoading] = useState(true);
  const [metaData, setMetaData] = useState([]);
  const [logs, setLogs] = useState({});

  useEffect(() => {
    fetch('api/logs')
      .then(response => response.json())
      .then(data => {
        setMetaData(data);
        setLoading(false);
      }).catch(() => { throw "" });
  }, []);

  useInterval(async () => {
    if (loading) { return; }

    for (let meta of metaData) {
      await fetch(`api/logs/${meta.id}`)
        .then(response => response.text())
        .then(data => {
          setLogs(logs => ({ ...logs, [meta.id]: data }));
        });
    }
  }, 5000);

  let contents = loading
    ? <p><em>Loading...</em></p>
    : (
      <div>
        <Nav vertical pills>
          {
            metaData.map(meta =>
              <NavItem>
                <NavLink
                  className={classnames({ active: meta.id === packageId ?? metaData[0] })}
                  onClick={() => { history.push(`/logs/${meta.id}`); }}
                >
                {meta.package} <em>{meta.id}</em>
                </NavLink>
              </NavItem>)
          }
        </Nav>
        <TabContent activeTab={packageId ?? metaData[0]}>
          {
            metaData.map(meta =>
              <TabPane tabId={meta.id}>
                {
                  <pre>{logs[meta.id]}</pre> ?? <p><em>Loading...</em></p>
                }
              </TabPane>
            )
          }
        </TabContent>
      </div>
    );

  return contents;
}