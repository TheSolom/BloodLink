import { App } from "@/app/App";
import { Providers } from "@/app/providers";
import * as React from "react";
import * as ReactDOM from "react-dom/client";
import "@/index.css";

const rootElement = document.getElementById("root");
if (rootElement) {
  ReactDOM.createRoot(rootElement).render(
    <React.StrictMode>
      <Providers>
        <App />
      </Providers>
    </React.StrictMode>,
  );
}
