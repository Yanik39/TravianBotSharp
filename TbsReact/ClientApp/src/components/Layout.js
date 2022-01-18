import React from "react";
import NavMenu from "./NavMenu";

// Router
import {
	BrowserRouter as Router,
	Switch,
	Route,
	Redirect,
} from "react-router-dom";

// info view
import Info from "./Views/Info";

// debug view
import Debug from "./Views/Debug";

import Setting from "./Views/Setting";

const Layout = () => {
	return (
		<Router>
			<NavMenu />
			<div style={{ margin: "1%" }}>
				<Switch>
					<Route path={"/setting"}>
						<Setting />
					</Route>
					<Route path={"/debug"}>
						<Debug />
					</Route>
					<Route path={"/info"}>
						<Info />
					</Route>
					<Route path="*">
						<Redirect to="/info" />
					</Route>
				</Switch>
			</div>
		</Router>
	);
};

export default Layout;
