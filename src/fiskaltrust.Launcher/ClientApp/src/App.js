import React, { Component } from 'react';
import { Route } from 'react-router';
import { Layout } from './components/Layout';
import { Home } from './components/Home';
import { LauncherConfig } from './components/LauncherConfig';
import { CashboxConfig } from './components/CashboxConfig';
import { Logs } from './components/Logs';

import './custom.css'

export default class App extends Component {
  static displayName = App.name;

  render () {
    return (
      <Layout>
        <Route exact path='/' component={Home} />
        <Route path='/config/launcher' component={LauncherConfig} />
        <Route path='/config/cashbox' component={CashboxConfig} />
        <Route path='/logs/:packageId?' component={Logs} />
      </Layout>
    );
  }
}
