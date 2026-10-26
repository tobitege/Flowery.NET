# syntax=docker/dockerfile:1.7

# A Linux desktop (XFCE) for running .NET UI apps, such as the Flowery Galleries, viewed in a browser through noVNC.
# Flowery.NET and Flowery.Uno keep this folder the same and build the image under the same tag; change both together.
#
# The image holds no .NET SDK or app: publish the app self-contained for Linux, mount it at /opt/app and pass the path of
# its executable as APP_PATH (scripts/run-linux-desktop.ps1 of each repository does that). Build it with:
#   docker build -t flowery-linux-desktop docker/linux-desktop
FROM debian:bookworm-slim

ARG DEBIAN_FRONTEND=noninteractive

# XFCE as the desktop, TigerVNC as its X server, noVNC to show it in a browser. The apps need X11 with its input and
# cursor extensions, fontconfig with fonts (an emoji font too, the Galleries show emoji), and ICU for globalization; Mesa
# gives OpenGL without a GPU. xwd with netpbm takes PNG screenshots and xdotool drives input, for checks from outside the
# container.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        xfce4 xfce4-terminal dbus-x11 \
        tigervnc-standalone-server tigervnc-tools novnc python3-websockify \
        libx11-6 libice6 libsm6 libxi6 libxcursor1 libxrandr2 libxext6 libxrender1 \
        libfontconfig1 fonts-dejavu-core fonts-noto-core fonts-noto-color-emoji libicu72 \
        libgl1 libegl1 libgl1-mesa-dri \
        x11-apps netpbm xdotool procps ca-certificates \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --create-home --shell /bin/bash flowery

# The root of noVNC opens the desktop instead of a directory listing.
RUN echo '<!DOCTYPE html><meta http-equiv="refresh" content="0; url=vnc.html?autoconnect=true&amp;resize=scale">' \
        > /usr/share/novnc/index.html

COPY start-desktop.sh /usr/local/bin/start-desktop.sh
# The script may arrive with Windows line endings from a checkout on Windows.
RUN sed -i 's/\r$//' /usr/local/bin/start-desktop.sh && chmod +x /usr/local/bin/start-desktop.sh

USER flowery
WORKDIR /home/flowery
# English unless the run passes another culture as LANG; the image has no locales, so only the app follows it.
ENV DISPLAY=:1 \
    LANG=en_US.UTF-8

# 6080: noVNC in a browser; 5901: VNC for a native viewer.
EXPOSE 6080 5901

ENTRYPOINT ["/usr/local/bin/start-desktop.sh"]
