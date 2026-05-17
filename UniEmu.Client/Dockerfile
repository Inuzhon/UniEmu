# Stage 1: Build the application
FROM node:20-alpine as builder
WORKDIR /app

# Copy package.json and package-lock.json first to leverage Docker cache
COPY package*.json ./

# Install dependencies
RUN npm install

# Copy the rest of the application source code
COPY . .

# Build the application (assuming 'npm run build' creates the optimized static files in /dist)
RUN npm run build

# Stage 2: Serve the application using Nginx
FROM nginx:stable-alpine
# Copy the built assets from the builder stage to Nginx's default web root
COPY --from=builder /app/dist /usr/share/nginx/html
# Expose the port the application runs on
EXPOSE 3000
# Start Nginx
CMD ["nginx", "-g", "daemon off;"]